// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class HttpInvokerProcess : WorkerProcessBase
    {
        private readonly IWorkerProcessFactory _processFactory;
        private readonly ILogger _workerProcessLogger;
        private readonly IScriptEventManager _eventManager;

        private string _workerId;
        private string _scriptRootPath;
        private WorkerProcessArguments _workerProcessArguments;

        internal HttpInvokerProcess(string workerId,
                                       string rootScriptPath,
                                       WorkerProcessArguments workerProcessArguments,
                                       IScriptEventManager eventManager,
                                       IWorkerProcessFactory processFactory,
                                       IProcessRegistry processRegistry,
                                       ILogger workerProcessLogger,
                                       ILanguageWorkerConsoleLogSource consoleLogSource)
            : base(workerId, rootScriptPath, workerProcessArguments, eventManager, processFactory, processRegistry, workerProcessLogger, consoleLogSource)
        {
            _processFactory = processFactory;
            _eventManager = eventManager;
            _workerProcessLogger = workerProcessLogger;
            _workerId = workerId;
            _scriptRootPath = rootScriptPath;
            _workerProcessArguments = workerProcessArguments;
        }

        internal override Process CreateWorkerProcess()
        {
            var workerContext = new WorkerContext()
            {
                RequestId = Guid.NewGuid().ToString(),
                WorkerId = _workerId,
                Arguments = _workerProcessArguments,
                WorkingDirectory = _scriptRootPath,
            };
            return _processFactory.CreateWorkerProcess(workerContext);
        }

        internal override void HandleWorkerProcessExitError(LanguageWorkerProcessExitException langExc)
        {
            // The subscriber of WorkerErrorEvent is expected to Dispose() the errored channel
            if (langExc != null && langExc.ExitCode != -1)
            {
                _workerProcessLogger.LogDebug(langExc, $"Language Worker Process exited.", _workerProcessArguments.ExecutablePath);
                _eventManager.Publish(new HttpWorkerErrorEvent(_workerId, langExc));
            }
        }

        internal override void HandleWorkerProcessRestart()
        {
            _workerProcessLogger?.LogInformation("Language Worker Process exited and needs to be restarted.");
            _eventManager.Publish(new HttpWorkerRestartEvent(_workerId));
        }
    }
}