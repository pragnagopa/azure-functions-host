// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    public class HttpWorkerChannelExt : IHttpWorkerChannel, IDisposable
    {
        private bool _disposed;
        private ILogger _workerChannelLogger;
        private IHttpWorkerService _httpWorkerService;

        internal HttpWorkerChannelExt(
           string workerId,
           IHttpWorkerService httpInvokerService,
           ILogger logger,
           IMetricsLogger metricsLogger)
        {
            Id = workerId;
            _workerChannelLogger = logger;
            _httpWorkerService = httpInvokerService;
            // _startLatencyMetric = metricsLogger?.LatencyEvent(string.Format(MetricEventNames.WorkerInitializeLatency, "HttpInvoker"));
        }

        public string Id { get; }

        public Task InvokeFunction(ScriptInvocationContext context)
        {
            return _httpWorkerService.InvokeAsync(context);
        }

        public Task StartWorkerProcessAsync()
        {
            _workerChannelLogger.LogDebug("Initiating Worker Process start up");
            // await _languageWorkerProcess.StartProcessAsync();
            return Task.CompletedTask;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // _startLatencyMetric?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
