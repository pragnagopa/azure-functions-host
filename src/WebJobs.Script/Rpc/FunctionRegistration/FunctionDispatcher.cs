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
        private readonly int _debounceMilliseconds = 5000;
        private IScriptEventManager _eventManager;
        private IEnumerable<WorkerConfig> _workerConfigs;
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

        internal int MaxProcessCount => _maxProcessCount;

        internal ILanguageWorkerChannelManager ChannelManager => _languageWorkerChannelManager;

        internal void InitializeJobHostLanguageWorkerChannel(int attemptCount)
        {
            var languageWorkerChannel = _languageWorkerChannelManager.CreateLanguageWorkerChannel(Guid.NewGuid().ToString(), _scriptOptions.RootScriptPath, _workerRuntime, _metricsLogger, attemptCount, false, _managedDependencyOptions);
            languageWorkerChannel.StartWorkerProcess();
        }

        internal void InitializeJobhostLanguageWorkerChannel()
        {
            InitializeJobHostLanguageWorkerChannel(0);
        }

        internal async void InitializeWebhostLanguageWorkerChannel()
        {
            _logger.LogDebug("Creating new webhost language worker channel for runtime:{workerRuntime}.", _workerRuntime);
            ILanguageWorkerChannel workerChannel = await _languageWorkerChannelManager.InitializeChannelAsync(_workerRuntime);
            workerChannel.RegisterFunctions(_workerState.Functions);
        }

        private void DebouceStartWorkerProcesses(int startIndex, Action targetAction)
        {
            for (var count = startIndex; count < _maxProcessCount; count++)
            {
                Action startWorkerChannelAction = targetAction;
                startWorkerChannelAction = startWorkerChannelAction.Debounce(count * _debounceMilliseconds);
                startWorkerChannelAction();
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

        public void Initialize(string workerRuntime, IEnumerable<FunctionMetadata> functions)
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
                        initializedChannel.RegisterFunctions(_workerState.Functions);
                    }
                    DebouceStartWorkerProcesses(initializedChannels.Count(), InitializeWebhostLanguageWorkerChannel);
                }
                else
                {
                    InitializeJobhostLanguageWorkerChannel();
                    DebouceStartWorkerProcesses(1, InitializeJobhostLanguageWorkerChannel);
                }
            }
        }

        public void Register(FunctionMetadata context)
        {
            _workerState.Functions.OnNext(context);
        }

        public async void Invoke(ScriptInvocationContext invocationContext)
        {
            if (_workerState.ProcessRestartCountExceedException != null)
            {
                invocationContext.ResultSource.TrySetException(_workerState.ProcessRestartCountExceedException);
                return;
            }
            IEnumerable<ILanguageWorkerChannel> workerChannels = GetWorkerChannels();
            if (workerChannels.Count() == 0)
            {
                await Utility.DelayAsync(TimeSpan.FromMilliseconds(30), 10, () =>
                {
                    workerChannels = GetWorkerChannels();
                    return workerChannels.Count() != 0;
                });
            }

            var languageWorkerChannel = _functionDispatcherLoadBalancer.GetLanguageWorkerChannel(workerChannels);
            _logger.LogDebug($"Sending invocation id:{invocationContext.ExecutionContext.InvocationId}");
            languageWorkerChannel.SendInvocationRequest(invocationContext);
        }

        private IEnumerable<ILanguageWorkerChannel> GetWorkerChannels()
        {
            var webhostChannels = _languageWorkerChannelManager.GetChannels(_workerRuntime);
            var workerChannels = webhostChannels == null ? _workerState.GetChannels() : webhostChannels.Union(_workerState.GetChannels());
            return workerChannels;
        }

        public void WorkerError(WorkerErrorEvent workerError)
        {
            _logger.LogDebug("Handling WorkerErrorEvent for runtime:{runtime}, workerId:{workerId}", workerError.Language, workerError.WorkerId);
            _workerState.Errors.Add(workerError.Exception);
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
            if (_workerState.Errors.Count < 3)
            {
                InitializeJobHostLanguageWorkerChannel(_workerState.Errors.Count);
            }
            else if (_workerState.GetChannels().Count() == 0)
            {
                _logger.LogDebug("Exceeded language worker restart retry count for runtime:{runtime}", runtime);
                PublishWorkerProcessErrorEvent(runtime);
            }
        }

        private void PublishWorkerProcessErrorEvent(string runtime)
        {
            var exMessage = $"Failed to start language worker process for: {runtime}";
            _workerState.ProcessRestartCountExceedException = new LanguageWorkerChannelException(exMessage);
            _eventManager.Publish(new WorkerProcessErrorEvent(runtime, _workerState.ProcessRestartCountExceedException));
        }

        private void AddOrUpdateWorkerChannels(RpcJobHostChannelReadyEvent rpcChannelReadyEvent)
        {
            // TODO: pgopa figure out if state is needed
            _logger.LogInformation("Adding jobhost language worker channel for runtime: {language}.", rpcChannelReadyEvent.Language);
            rpcChannelReadyEvent.LanguageWorkerChannel.RegisterFunctions(_workerState.Functions);
            _workerState.AddChannel(rpcChannelReadyEvent.LanguageWorkerChannel);
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
