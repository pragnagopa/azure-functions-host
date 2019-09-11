// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FunctionMetadata = Microsoft.Azure.WebJobs.Script.Description.FunctionMetadata;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class HttpFunctionInvokeDispatcher : IFunctionDispatcher
    {
        private readonly IMetricsLogger _metricsLogger;
        private readonly ILogger _logger;
        private readonly ILanguageWorkerChannelFactory _languageWorkerChannelFactory;
        private readonly IEnvironment _environment;
        private readonly IScriptJobHostEnvironment _scriptJobHostEnvironment;
        private readonly TimeSpan thresholdBetweenRestarts = TimeSpan.FromMinutes(LanguageWorkerConstants.WorkerRestartErrorIntervalThresholdInMinutes);

        private IScriptEventManager _eventManager;
        private IEnumerable<WorkerConfig> _workerConfigs;
        private IJobHostLanguageWorkerChannelManager _jobHostLanguageWorkerChannelManager;
        private IDisposable _workerErrorSubscription;
        private IDisposable _workerRestartSubscription;
        private ScriptJobHostOptions _scriptOptions;
        private bool _disposed = false;
        private bool _disposing = false;
        private string _workerRuntime;
        private IEnumerable<FunctionMetadata> _functions;
        private ConcurrentStack<WorkerErrorEvent> _languageWorkerErrors = new ConcurrentStack<WorkerErrorEvent>();

        public HttpFunctionInvokeDispatcher(IOptions<ScriptJobHostOptions> scriptHostOptions,
            IMetricsLogger metricsLogger,
            IEnvironment environment,
            IScriptJobHostEnvironment scriptJobHostEnvironment,
            IScriptEventManager eventManager,
            ILoggerFactory loggerFactory,
            ILanguageWorkerChannelFactory languageWorkerChannelFactory,
            IOptions<LanguageWorkerOptions> languageWorkerOptions,
            IJobHostLanguageWorkerChannelManager jobHostLanguageWorkerChannelManager)
        {
            _metricsLogger = metricsLogger;
            _scriptOptions = scriptHostOptions.Value;
            _environment = environment;
            _scriptJobHostEnvironment = scriptJobHostEnvironment;
            _jobHostLanguageWorkerChannelManager = jobHostLanguageWorkerChannelManager;
            _eventManager = eventManager;
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _logger = loggerFactory.CreateLogger<HttpFunctionInvokeDispatcher>();
            _languageWorkerChannelFactory = languageWorkerChannelFactory;
            _workerRuntime = _environment.GetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeSettingName);

            State = FunctionDispatcherState.Default;

            _workerErrorSubscription = _eventManager.OfType<WorkerErrorEvent>()
               .Subscribe(WorkerError);
            _workerRestartSubscription = _eventManager.OfType<WorkerRestartEvent>()
               .Subscribe(WorkerRestart);
        }

        public FunctionDispatcherState State { get; private set; }

        public IJobHostLanguageWorkerChannelManager JobHostLanguageWorkerChannelManager => _jobHostLanguageWorkerChannelManager;

        internal ConcurrentStack<WorkerErrorEvent> LanguageWorkerErrors => _languageWorkerErrors;

        internal async void InitializeJobhostLanguageWorkerChannelAsync()
        {
            await InitializeJobhostLanguageWorkerChannelAsync(0);
        }

        internal Task InitializeJobhostLanguageWorkerChannelAsync(int attemptCount)
        {
            // TODO: Add process managment for http invoker
            // Define _jobHostLanguageWorkerChannelManager to for managing http invoker channels
            //var languageWorkerChannel = _languageWorkerChannelFactory.CreateLanguageWorkerChannel(_scriptOptions.RootScriptPath, _workerRuntime, _metricsLogger, attemptCount);
            //languageWorkerChannel.SetupFunctionInvocationBuffers(_functions);
            //_jobHostLanguageWorkerChannelManager.AddChannel(languageWorkerChannel);
            //languageWorkerChannel.StartWorkerProcessAsync().ContinueWith(workerInitTask =>
            //     {
            //         if (workerInitTask.IsCompleted)
            //         {
            //             _logger.LogDebug("Adding jobhost language worker channel for runtime: {language}. workerId:{id}", _workerRuntime, languageWorkerChannel.Id);
            //             languageWorkerChannel.SendFunctionLoadRequests();
            //             State = FunctionDispatcherState.Initialized;
            //         }
            //         else
            //         {
            //             _logger.LogWarning("Failed to start language worker process for runtime: {language}. workerId:{id}", _workerRuntime, languageWorkerChannel.Id);
            //         }
            //     });
            return Task.CompletedTask;
        }

        public bool IsSupported(FunctionMetadata functionMetadata, string workerRuntime)
        {
            return true;
        }

        public async Task InitializeAsync(IEnumerable<FunctionMetadata> functions)
        {
            _functions = functions;

            if (functions == null || functions.Count() == 0)
            {
                // do not initialize function dispachter if there are no functions
                return;
            }

            State = FunctionDispatcherState.Initializing;
            await InitializeJobhostLanguageWorkerChannelAsync(0);
        }

        public Task InvokeAsync(ScriptInvocationContext invocationContext)
        {
            try
            {
                // TODO: convert send ScriptInvocationContext to http request
            }
            catch (Exception invokeEx)
            {
                invocationContext.ResultSource.TrySetException(invokeEx);
            }
            return Task.CompletedTask;
        }

        public async void WorkerError(WorkerErrorEvent workerError)
        {
            if (!_disposing)
            {
                _logger.LogDebug("Handling WorkerErrorEvent for runtime:{runtime}, workerId:{workerId}", workerError.Language, workerError.WorkerId);
                AddOrUpdateErrorBucket(workerError);
                await DisposeAndRestartWorkerChannel(workerError.Language, workerError.WorkerId);
            }
        }

        public async void WorkerRestart(WorkerRestartEvent workerRestart)
        {
            if (!_disposing)
            {
                _logger.LogDebug("Handling WorkerRestartEvent for runtime:{runtime}, workerId:{workerId}", workerRestart.Language, workerRestart.WorkerId);
                await DisposeAndRestartWorkerChannel(workerRestart.Language, workerRestart.WorkerId);
            }
        }

        private async Task DisposeAndRestartWorkerChannel(string runtime, string workerId)
        {
                _logger.LogDebug("Disposing channel for workerId: {channelId}, for runtime:{language}", workerId, runtime);
                var channel = _jobHostLanguageWorkerChannelManager.GetChannels().Where(ch => ch.Id == workerId).FirstOrDefault();
                if (channel != null)
                {
                    _jobHostLanguageWorkerChannelManager.DisposeAndRemoveChannel(channel);
                }
                _logger.LogDebug("Restarting worker channel for runtime:{runtime}", runtime);
                await RestartWorkerChannel(runtime, workerId);
        }

        private async Task RestartWorkerChannel(string runtime, string workerId)
        {
            if (_languageWorkerErrors.Count < 3)
            {
                await InitializeJobhostLanguageWorkerChannelAsync(_languageWorkerErrors.Count);
            }
            else if (_jobHostLanguageWorkerChannelManager.GetChannels().Count() == 0)
            {
                _logger.LogError("Exceeded language worker restart retry count for runtime:{runtime}. Shutting down Functions Host", runtime);
                _scriptJobHostEnvironment.Shutdown();
            }
        }

        private void AddOrUpdateErrorBucket(WorkerErrorEvent currentErrorEvent)
        {
            if (_languageWorkerErrors.TryPeek(out WorkerErrorEvent top))
            {
                if ((currentErrorEvent.CreatedAt - top.CreatedAt) > thresholdBetweenRestarts)
                {
                    while (!_languageWorkerErrors.IsEmpty)
                    {
                        _languageWorkerErrors.TryPop(out WorkerErrorEvent popped);
                        _logger.LogDebug($"Popping out errorEvent createdAt:{popped.CreatedAt} workerId:{popped.WorkerId}");
                    }
                }
            }
            _languageWorkerErrors.Push(currentErrorEvent);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _logger.LogDebug("Disposing FunctionDispatcher");
                _workerErrorSubscription.Dispose();
                _workerRestartSubscription.Dispose();
                _jobHostLanguageWorkerChannelManager.DisposeAndRemoveChannels();
                _disposed = true;
            }
        }

        public void Dispose()
        {
            _disposing = true;
            Dispose(true);
        }
    }
}
