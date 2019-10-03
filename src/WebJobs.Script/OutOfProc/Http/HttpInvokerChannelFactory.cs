// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    public class HttpInvokerChannelFactory : IHttpInvokerChannelFactory
    {
        private readonly ILoggerFactory _loggerFactory = null;
        private readonly IHttpInvokerProcessFactory _httpInvokerProcessFactory = null;
        private readonly IScriptEventManager _eventManager = null;
        private readonly IEnumerable<WorkerConfig> _workerConfigs = null;
        private IHttpInvokerService _httpInvokerService;

        public HttpInvokerChannelFactory(IScriptEventManager eventManager, IEnvironment environment, ILoggerFactory loggerFactory, IOptions<LanguageWorkerOptions> languageWorkerOptions,
            IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions, IHttpInvokerProcessFactory httpInvokerProcessFactory, IHttpInvokerService httpInvokerService)
        {
            _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _httpInvokerProcessFactory = httpInvokerProcessFactory ?? throw new ArgumentNullException(nameof(httpInvokerProcessFactory));
            _httpInvokerService = httpInvokerService ?? throw new ArgumentNullException(nameof(httpInvokerService));
        }

        public IHttpInvokerChannel Create(string scriptRootPath, IMetricsLogger metricsLogger, int attemptCount)
        {
            if (_workerConfigs == null)
            {
                throw new InvalidOperationException($"Did not find worker config for HttpInvoker");
            }
            if (_workerConfigs.Count() > 1)
            {
                throw new InvalidOperationException($"Found more than one HttpInvoker config");
            }
            string workerId = Guid.NewGuid().ToString();
            ILogger workerLogger = _loggerFactory.CreateLogger($"Worker.HttpInvokerChannel.{workerId}");
            ILanguageWorkerProcess httpInokerProcess = _httpInvokerProcessFactory.Create(workerId, scriptRootPath, _workerConfigs.ElementAt(0));
            return new HttpInvokerChannel(
                         workerId,
                         scriptRootPath,
                         _eventManager,
                         httpInokerProcess,
                         _httpInvokerService,
                         workerLogger,
                         metricsLogger,
                         attemptCount);
        }
    }
}
