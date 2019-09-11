﻿// Copyright (c) .NET Foundation. All rights reserved.
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
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FunctionMetadata = Microsoft.Azure.WebJobs.Script.Description.FunctionMetadata;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    internal class HttpFunctionInvokeDispatcher : IFunctionDispatcher
    {
        private readonly IMetricsLogger _metricsLogger;
        private readonly ILogger _logger;
        private readonly IHttpInvokerChannelFactory _httpInvokerChannelFactory;
        private readonly IEnvironment _environment;
        private readonly IScriptJobHostEnvironment _scriptJobHostEnvironment;
        private readonly TimeSpan thresholdBetweenRestarts = TimeSpan.FromMinutes(LanguageWorkerConstants.WorkerRestartErrorIntervalThresholdInMinutes);

        private IScriptEventManager _eventManager;
        private IEnumerable<WorkerConfig> _workerConfigs;
        private IDisposable _workerErrorSubscription;
        private IDisposable _workerRestartSubscription;
        private ScriptJobHostOptions _scriptOptions;
        private bool _disposed = false;
        private bool _disposing = false;
        private IEnumerable<FunctionMetadata> _functions;
        private ConcurrentStack<HttpWorkerErrorEvent> _languageWorkerErrors = new ConcurrentStack<HttpWorkerErrorEvent>();
        private IHttpInvokerChannel _httpInvokerChannel;

        public HttpFunctionInvokeDispatcher(IOptions<ScriptJobHostOptions> scriptHostOptions,
            IMetricsLogger metricsLogger,
            IEnvironment environment,
            IScriptJobHostEnvironment scriptJobHostEnvironment,
            IScriptEventManager eventManager,
            ILoggerFactory loggerFactory,
            IHttpInvokerChannelFactory httpInvokerChannelFactory,
            IOptions<LanguageWorkerOptions> languageWorkerOptions,
            IJobHostLanguageWorkerChannelManager jobHostLanguageWorkerChannelManager)
        {
            _metricsLogger = metricsLogger;
            _scriptOptions = scriptHostOptions.Value;
            _environment = environment;
            _scriptJobHostEnvironment = scriptJobHostEnvironment;
            _eventManager = eventManager;
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _logger = loggerFactory.CreateLogger<HttpFunctionInvokeDispatcher>();
            _httpInvokerChannelFactory = httpInvokerChannelFactory;

            State = FunctionDispatcherState.Default;

            _workerErrorSubscription = _eventManager.OfType<HttpWorkerErrorEvent>()
               .Subscribe(WorkerError);
            _workerRestartSubscription = _eventManager.OfType<HttpWorkerRestartEvent>()
               .Subscribe(WorkerRestart);
        }

        public FunctionDispatcherState State { get; private set; }

        internal ConcurrentStack<HttpWorkerErrorEvent> LanguageWorkerErrors => _languageWorkerErrors;

        internal async void InitializeJobhostLanguageWorkerChannelAsync()
        {
            await InitializeJobhostLanguageWorkerChannelAsync(0);
        }

        internal Task InitializeJobhostLanguageWorkerChannelAsync(int attemptCount)
        {
            // TODO: Add process managment for http invoker
            _httpInvokerChannel = _httpInvokerChannelFactory.CreateHttpInvokerChannel(_scriptOptions.RootScriptPath, _metricsLogger, attemptCount);
            _httpInvokerChannel.StartWorkerProcessAsync().ContinueWith(workerInitTask =>
                 {
                     if (workerInitTask.IsCompleted)
                     {
                         _logger.LogDebug("Adding http invoker channel. workerId:{id}", _httpInvokerChannel.Id);
                         // TODO: wait for status api ok
                         State = FunctionDispatcherState.Initialized;
                     }
                     else
                     {
                         _logger.LogWarning("Failed to start http invoker process. workerId:{id}", _httpInvokerChannel.Id);
                     }
                 });
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

        public async void WorkerError(HttpWorkerErrorEvent workerError)
        {
            if (!_disposing)
            {
                _logger.LogDebug("Handling WorkerErrorEvent for workerId:{workerId}", workerError.WorkerId);
                AddOrUpdateErrorBucket(workerError);
                await DisposeAndRestartWorkerChannel(workerError.WorkerId);
            }
        }

        public async void WorkerRestart(HttpWorkerRestartEvent workerRestart)
        {
            if (!_disposing)
            {
                _logger.LogDebug("Handling WorkerRestartEvent for workerId:{workerId}", workerRestart.WorkerId);
                await DisposeAndRestartWorkerChannel(workerRestart.WorkerId);
            }
        }

        private async Task DisposeAndRestartWorkerChannel(string workerId)
        {
                _logger.LogDebug("Disposing channel for workerId: {channelId}", workerId);
                if (_httpInvokerChannel != null)
                {
                    (_httpInvokerChannel as IDisposable)?.Dispose();
                }
                _logger.LogDebug("Restarting http invoker channel");
                await RestartWorkerChannel(workerId);
        }

        private async Task RestartWorkerChannel(string workerId)
        {
            if (_languageWorkerErrors.Count < 3)
            {
                await InitializeJobhostLanguageWorkerChannelAsync(_languageWorkerErrors.Count);
            }
            else if (_httpInvokerChannel == null)
            {
                _logger.LogError("Exceeded http invoker restart retry count. Shutting down Functions Host");
                _scriptJobHostEnvironment.Shutdown();
            }
        }

        private void AddOrUpdateErrorBucket(HttpWorkerErrorEvent currentErrorEvent)
        {
            if (_languageWorkerErrors.TryPeek(out HttpWorkerErrorEvent top))
            {
                if ((currentErrorEvent.CreatedAt - top.CreatedAt) > thresholdBetweenRestarts)
                {
                    while (!_languageWorkerErrors.IsEmpty)
                    {
                        _languageWorkerErrors.TryPop(out HttpWorkerErrorEvent popped);
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
                _logger.LogDebug("Disposing HttpFunctionInvokeDispatcher");
                _workerErrorSubscription.Dispose();
                _workerRestartSubscription.Dispose();
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
