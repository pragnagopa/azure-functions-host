// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.ManagedDependencies;
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
        private int _maxProcessCount;
        private IFunctionDispatcherLoadBalancer _functionDispatcherLoadBalancer;
        private bool disposedValue = false;
        private IOptions<ManagedDependencyOptions> _managedDependencyOptions;
        private string _workerRuntime;

        public FunctionDispatcher(IOptions<ScriptJobHostOptions> scriptHostOptions,
            IMetricsLogger metricsLogger,
            IEnvironment environment,
            IScriptEventManager eventManager,
            ILoggerFactory loggerFactory,
            IOptions<LanguageWorkerOptions> languageWorkerOptions,
            ILanguageWorkerChannelManager languageWorkerChannelManager,
            IOptions<ManagedDependencyOptions> managedDependencyOptions)
        {
            _metricsLogger = metricsLogger;
            _scriptOptions = scriptHostOptions.Value;
            _environment = environment;
            _languageWorkerChannelManager = languageWorkerChannelManager;
            _eventManager = eventManager;
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _managedDependencyOptions = managedDependencyOptions;
            _logger = loggerFactory.CreateLogger<FunctionDispatcher>();

            var processCount = _environment.GetEnvironmentVariable(LanguageWorkerConstants.FunctionsWorkerProcessCountSettingName);
            _maxProcessCount = (processCount != null && int.Parse(processCount) > 1) ? int.Parse(processCount) : 1;
            _functionDispatcherLoadBalancer = new FunctionDispatcherLoadBalancer(_maxProcessCount);

            _workerErrorSubscription = _eventManager.OfType<WorkerErrorEvent>()
               .Subscribe(WorkerError);

            _rpcChannelReadySubscriptions = _eventManager.OfType<RpcJobHostChannelReadyEvent>().Subscribe(AddOrUpdateWorkerChannels);
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
                        var languageWorkerChannel = _languageWorkerChannelManager.CreateLanguageWorkerChannel(Guid.NewGuid().ToString(), _scriptOptions.RootScriptPath, language, _metricsLogger, attemptCount, false, _managedDependencyOptions);
                        languageWorkerChannel.StartWorkerProcess();
                        languageWorkerChannel.RegisterFunctions(registrations);
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

        public void RegisterFunctionsWithExistingChannel(ILanguageWorkerChannel languageWorkerChannel)
        {
            languageWorkerChannel.RegisterFunctions(_workerState.Functions);
        }

        public async void Initialize(string workerRuntime, IEnumerable<FunctionMetadata> functions)
        {
            _workerRuntime = workerRuntime;
            _languageWorkerChannelManager.ShutdownStandbyChannels(functions);

            if (Utility.IsSupportedRuntime(workerRuntime, _workerConfigs))
            {
                IEnumerable<ILanguageWorkerChannel> initializedChannels = _languageWorkerChannelManager.GetChannels(workerRuntime);
                if (initializedChannels != null)
                {
                    foreach (var initializedChannel in initializedChannels)
                    {
                        _logger.LogDebug("Found initialized language worker channel for runtime: {workerRuntime} workerId:{workerId}", workerRuntime, initializedChannel.Id);
                        RegisterFunctionsWithExistingChannel(initializedChannel);
                    }

                    for (var count = initializedChannels.Count(); count < _maxProcessCount; count++)
                    {
                        _logger.LogDebug("Creating new webhost language worker channel for runtime:{workerRuntime}. Count: {count}", workerRuntime, count);
                        ILanguageWorkerChannel workerChannel = await _languageWorkerChannelManager.InitializeChannelAsync(workerRuntime);
                        RegisterFunctionsWithExistingChannel(workerChannel);
                    }
                }
                else
                {
                    for (var count = 0; count < _maxProcessCount; count++)
                    {
                        _logger.LogDebug("Creating new Jobhost language worker channel for runtime:{workerRuntime}. Count: {count}", workerRuntime, count);
                        CreateWorkerState(workerRuntime);
                    }
                }
            }
        }

        private void CreateWorkerState(string runtime)
        {
            WorkerConfig config = _workerConfigs.Where(c => c.Language.Equals(runtime, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            var languageWorkerChannel = ChannelFactory(runtime, _workerState.Functions, 0);
        }

        public void Register(FunctionMetadata context)
        {
            _workerState.Functions.OnNext(context);
        }

        public void Invoke(ScriptInvocationContext invocationContext)
        {
            if (_workerState.ProcessRestartCountExceedException != null)
            {
                invocationContext.ResultSource.TrySetException(_workerState.ProcessRestartCountExceedException);
                return;
            }
            var webhostChannels = _languageWorkerChannelManager.GetChannels(_workerRuntime);
            var workerChannels = webhostChannels == null ? _workerState.GetChannels() : webhostChannels.Union(_workerState.GetChannels());
            var languageWorkerChannel = _functionDispatcherLoadBalancer.GetLanguageWorkerChannel(workerChannels);
            languageWorkerChannel.SendInvocationRequest(invocationContext);
        }

        public void WorkerError(WorkerErrorEvent workerError)
        {
            _logger.LogDebug("Handling WorkerErrorEvent for runtime:{runtime}", workerError.Language);
            if (_workerState.Errors.TryGetValue(workerError.WorkerId, out List<Exception> errorsList))
            {
                errorsList.Add(workerError.Exception);
            }
            else
            {
                _workerState.Errors.Add(workerError.WorkerId, new List<Exception>()
                {
                    workerError.Exception
                });
            }
            bool isPreInitializedChannel = _languageWorkerChannelManager.ShutdownChannelsIfExist(workerError.Language);
            if (!isPreInitializedChannel)
            {
                _logger.LogDebug("Disposing errored channel for workerId: {channelId}, for runtime:{language}", workerError.WorkerId, workerError.Language);
                var erroredChannel = _workerState.GetChannels().Where(ch => ch.Id == workerError.WorkerId).FirstOrDefault();
                _workerState.DisposeAndRemoveChannel(erroredChannel);
            }
            _logger.LogDebug("Restarting worker channel for runtime:{runtime}", workerError.Language);
            RestartWorkerChannel(workerError.Language, workerError.WorkerId);
        }

        private void RestartWorkerChannel(string runtime, string workerId)
        {
            if (_workerState.Errors[workerId].Count < 3)
            {
                _workerState.AddChannel(CreateNewChannelWithExistingWorkerState(runtime));
            }
            else if (_workerState.GetChannels().Count() == 0)
            {
                _logger.LogDebug("Exceeded language worker restart retry count for runtime:{runtime}", runtime);
                PublishWorkerProcessErrorEvent(runtime);
            }
        }

        private void PublishWorkerProcessErrorEvent(string runtime)
        {
            var exMessage = $"Failed to start language worker for: {runtime}";
            _workerState.ProcessRestartCountExceedException = new LanguageWorkerChannelException(exMessage);
            _eventManager.Publish(new WorkerProcessErrorEvent(runtime, _workerState.ProcessRestartCountExceedException));
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
                    foreach (var channel in _workerState.GetChannels())
                    {
                        channel.Dispose();
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
