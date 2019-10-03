// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc.Http
{
    internal class HttpInvokerProcessFactory : IHttpInvokerProcessFactory
    {
        private readonly IWorkerProcessFactory _workerProcessFactory;
        private readonly IProcessRegistry _processRegistry;
        private readonly ILoggerFactory _loggerFactory = null;
        private readonly IScriptEventManager _eventManager = null;
        private readonly ILanguageWorkerConsoleLogSource _consoleLogSource;

        public HttpInvokerProcessFactory(IScriptEventManager eventManager,
                                       ILoggerFactory loggerFactory,
                                       IWorkerProcessFactory defaultWorkerProcessFactory,
                                       IProcessRegistry processRegistry,
                                       ILanguageWorkerConsoleLogSource consoleLogSource)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
            _consoleLogSource = consoleLogSource ?? throw new ArgumentNullException(nameof(consoleLogSource));
            _workerProcessFactory = defaultWorkerProcessFactory ?? throw new ArgumentNullException(nameof(defaultWorkerProcessFactory));
            _processRegistry = processRegistry ?? throw new ArgumentNullException(nameof(processRegistry));
        }

        public ILanguageWorkerProcess Create(string workerId, string scriptRootPath, WorkerConfig workerConfig)
        {
            ILogger workerProcessLogger = _loggerFactory.CreateLogger($"Worker.HttpInvokerProcess.{workerId}");
            return new HttpInvokerProcess(workerId, scriptRootPath, workerArgs, _eventManager, _workerProcessFactory, _processRegistry, workerProcessLogger, _consoleLogSource);
        }
    }
}
