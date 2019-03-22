﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FunctionMetadata = Microsoft.Azure.WebJobs.Script.Description.FunctionMetadata;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class FunctionDispatcher : IFunctionDispatcher
    {
        private readonly IMetricsLogger _metricsLogger;
        private readonly ILogger _logger;
        private readonly IEnvironment _environment;
        private readonly IDisposable _rpcChannelReadySubscriptions;
        private IScriptEventManager _eventManager;
        private IEnumerable<WorkerConfig> _workerConfigs;
        private CreateChannel _channelFactory;
        private ILanguageWorkerChannelManager _languageWorkerChannelManager;
        private LanguageWorkerState _workerState = new LanguageWorkerState();
        private IDisposable _workerErrorSubscription;
        private IList<IDisposable> _workerStateSubscriptions = new List<IDisposable>();
        private ScriptJobHostOptions _scriptOptions;
        private IObservable<RpcJobHostChannelReadyEvent> _rpcChannelReadyEvents;
        private int _maxProcessCount;
        private IFunctionDispatcherLoadBalancer _functionDispatcherLoadBalancer;
        private bool disposedValue = false;

        public FunctionDispatcher(IOptions<ScriptJobHostOptions> scriptHostOptions,
            IMetricsLogger metricsLogger,
            IEnvironment environment,
            IScriptEventManager eventManager,
            ILoggerFactory loggerFactory,
            IOptions<LanguageWorkerOptions> languageWorkerOptions,
            ILanguageWorkerChannelManager languageWorkerChannelManager)
        {
            _metricsLogger = metricsLogger;
            _scriptOptions = scriptHostOptions.Value;
            _environment = environment;
            _languageWorkerChannelManager = languageWorkerChannelManager;
            _eventManager = eventManager;
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _logger = loggerFactory.CreateLogger(ScriptConstants.LogCategoryFunctionDispatcher);
            var processCount = _environment.GetEnvironmentVariable(LanguageWorkerConstants.FunctionsWorkerProcessCountSettingName);
            _maxProcessCount = (processCount != null && int.Parse(processCount) > 1) ? int.Parse(processCount) : 1;
            _maxProcessCount = _maxProcessCount <= 0 ? 1 : _maxProcessCount;
            _functionDispatcherLoadBalancer = new FunctionDispatcherLoadBalancer(_maxProcessCount);

            _workerErrorSubscription = _eventManager.OfType<WorkerErrorEvent>()
               .Subscribe(WorkerError);

            _rpcChannelReadySubscriptions = _eventManager.OfType<RpcJobHostChannelReadyEvent>()
               .Subscribe(AddOrUpdateWorkerChannels);
        }

        public LanguageWorkerState LanguageWorkerChannelState => _workerState;

        internal List<Exception> Errors { get; set; } = new List<Exception>();

        internal CreateChannel ChannelFactory
        {
            get
            {
                if (_channelFactory == null)
                {
                    _channelFactory = (language, registrations, attemptCount) =>
                    {
                        var languageWorkerChannel = _languageWorkerChannelManager.CreateLanguageWorkerChannel(Guid.NewGuid().ToString(), _scriptOptions.RootScriptPath, language, registrations, _metricsLogger, attemptCount, false);
                        _rpcChannelReadyEvents = _eventManager.OfType<RpcJobHostChannelReadyEvent>()
                       .Where(evt => evt.WorkerId == languageWorkerChannel.Id);
                        languageWorkerChannel.StartWorkerProcess();
                        return languageWorkerChannel;
                    };
                }
                return _channelFactory;
            }
        }

        public bool IsSupported(FunctionMetadata functionMetadata, string workerRuntime)
        {
            if (string.IsNullOrEmpty(functionMetadata.Language))
            {
                return false;
            }
            if (string.IsNullOrEmpty(workerRuntime))
            {
                return true;
            }
            return functionMetadata.Language.Equals(workerRuntime, StringComparison.OrdinalIgnoreCase);
        }

        public void CreateWorkerStateWithExistingChannel(string language, ILanguageWorkerChannel languageWorkerChannel)
        {
            languageWorkerChannel.RegisterFunctions(_workerState.Functions);
        }

        public void Initialize(string workerRuntime, IEnumerable<FunctionMetadata> functions)
        {
            _languageWorkerChannelManager.ShutdownStandbyChannels(functions);

            if (Utility.IsSupportedRuntime(workerRuntime, _workerConfigs))
            {
                ILanguageWorkerChannel initializedChannel = _languageWorkerChannelManager.GetChannel(workerRuntime);
                if (initializedChannel != null)
                {
                    _logger.LogDebug("Found initialized language worker channel for runtime: {workerRuntime}", workerRuntime);
                    CreateWorkerStateWithExistingChannel(workerRuntime, initializedChannel);
                }
                else
                {
                    for (var count = 0; count < _maxProcessCount; count++)
                    {
                        _logger.LogDebug("Creating new language worker channel for runtime:{workerRuntime}. Count: {count}", workerRuntime, count);
                        CreateWorkerState(workerRuntime);
                    }
                }
            }
        }

        private void CreateWorkerState(string runtime)
        {
            WorkerConfig config = _workerConfigs.Where(c => c.Language.Equals(runtime, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            var languageWorkerChannel = ChannelFactory(runtime, _workerState.Functions, 0);
            _workerState.Channel = languageWorkerChannel;
        }

        public void Register(FunctionMetadata context)
        {
            _workerState.Functions.OnNext(context);
        }

        public void Invoke(ScriptInvocationContext invocationContext)
        {
            while (_workerState.GetChannels().Count() <= 0)
            {
                // Wait for response from language worker process
                // await _rpcChannelReadyEvents.FirstAsync();
            }
            using (_metricsLogger.LatencyEvent("FunctionDispatcher-Post", invocationContext.FunctionMetadata.Name))
            {
                var languageWorkerChannel = _functionDispatcherLoadBalancer.GetLanguageWorkerChannel(_workerState.GetChannels());
                BufferBlock<ScriptInvocationContext> bufferBlock = null;
                if (languageWorkerChannel.FunctionInputBuffers.TryGetValue(invocationContext.FunctionMetadata.FunctionId, out bufferBlock))
                {
                    _logger.LogInformation($"posting invocation id:{invocationContext.ExecutionContext.InvocationId} on threadid: {Thread.CurrentThread.ManagedThreadId}");
                    languageWorkerChannel.FunctionInputBuffers[invocationContext.FunctionMetadata.FunctionId].Post(invocationContext);
                }
                else
                {
                    throw new Exception("Function not loaded");
                }
            }
        }

        public void WorkerError(WorkerErrorEvent workerError)
        {
            _logger.LogDebug("Handling WorkerErrorEvent for runtime:{runtime}", workerError.Language);
            _workerState.Errors.Add(workerError.Exception);
            bool isPreInitializedChannel = _languageWorkerChannelManager.ShutdownChannelIfExists(workerError.Language);
            if (!isPreInitializedChannel)
            {
                _logger.LogDebug("Disposing errored channel for workerId: {channelId}, for runtime:{language}", _workerState.Channel.Id, workerError.Language);
                _workerState.Channel.Dispose();
            }
            _logger.LogDebug("Restarting worker channel for runtime:{runtime}", workerError.Language);
            RestartWorkerChannel(workerError.Language);
        }

        private void RestartWorkerChannel(string runtime)
        {
            if (_workerState.Errors.Count < 3)
            {
                _workerState.Channel = CreateNewChannelWithExistingWorkerState(runtime);
            }
            else
            {
                _logger.LogDebug("Exceeded language worker restart retry count for runtime:{runtime}", runtime);
                PublishWorkerProcessErrorEvent(runtime);
            }
        }

        private void PublishWorkerProcessErrorEvent(string runtime)
        {
            var exMessage = $"Failed to start language worker for: {runtime}";
            var languageWorkerChannelException = (_workerState.Errors != null && _workerState.Errors.Count > 0) ? new LanguageWorkerChannelException(exMessage, new AggregateException(_workerState.Errors.ToList())) : new LanguageWorkerChannelException(exMessage);
            var errorBlock = new ActionBlock<ScriptInvocationContext>(ctx =>
            {
                ctx.ResultSource.TrySetException(languageWorkerChannelException);
            });
            _workerStateSubscriptions.Add(_workerState.Functions.Subscribe(fm =>
            {
                _workerState.Channel.FunctionInputBuffers[fm.FunctionId].LinkTo(errorBlock);
            }));
            _eventManager.Publish(new WorkerProcessErrorEvent(_workerState.Channel.Id, runtime, languageWorkerChannelException));
        }

        private ILanguageWorkerChannel CreateNewChannelWithExistingWorkerState(string language)
        {
            WorkerConfig config = _workerConfigs.Where(c => c.Language.Equals(language, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            var newWorkerChannel = ChannelFactory(language, _workerState.Functions, _workerState.Errors.Count);
            newWorkerChannel.RegisterFunctions(_workerState.Functions);
            return newWorkerChannel;
        }

        private void AddOrUpdateWorkerChannels(RpcJobHostChannelReadyEvent rpcChannelReadyEvent)
        {
            _logger.LogInformation("Adding jobhost language worker channel for runtime: {language}.", rpcChannelReadyEvent.Language);
            _workerState.AddChannel(rpcChannelReadyEvent.LanguageWorkerChannel);
            rpcChannelReadyEvent.LanguageWorkerChannel.RegisterFunctions(_workerState.Functions);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _workerErrorSubscription.Dispose();
                    foreach (var subscription in _workerStateSubscriptions)
                    {
                        subscription.Dispose();
                    }
                    // TODO #3296 - send WorkerTerminate message to shut down language worker process gracefully (instead of just a killing)
                    // WebhostLanguageWorkerChannels life time is managed by LanguageWorkerChannelManager
                    if (_workerState.Channel != null && !_workerState.Channel.IsWebhostChannel)
                    {
                        _workerState.Channel.Dispose();
                    }
                    _workerState.Functions.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
