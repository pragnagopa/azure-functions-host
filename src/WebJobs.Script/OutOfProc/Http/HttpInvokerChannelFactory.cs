// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.OutOfProc.Http;
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
        private readonly HttpInvokerOptions _httpInvokerOptions = null;
        private IHttpInvokerService _httpInvokerService;

        public HttpInvokerChannelFactory(IScriptEventManager eventManager, IEnvironment environment, ILoggerFactory loggerFactory, IOptions<HttpInvokerOptions> httpInvokerOptions,
            IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions, IHttpInvokerProcessFactory httpInvokerProcessFactory, IHttpInvokerService httpInvokerService)
        {
            _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _httpInvokerOptions = httpInvokerOptions.Value ?? throw new ArgumentNullException(nameof(httpInvokerOptions.Value));
            _httpInvokerProcessFactory = httpInvokerProcessFactory ?? throw new ArgumentNullException(nameof(httpInvokerProcessFactory));
            _httpInvokerService = httpInvokerService ?? throw new ArgumentNullException(nameof(httpInvokerService));
        }

        public IHttpInvokerChannel Create(string scriptRootPath, IMetricsLogger metricsLogger, int attemptCount)
        {
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
