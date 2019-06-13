// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.ManagedDependencies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class LanguageWorkerChannelFactory : ILanguageWorkerChannelFactory
    {
        private readonly IEnumerable<WorkerConfig> _workerConfigs = null;
        private readonly ILogger _logger = null;
        private readonly IOptionsMonitor<ScriptApplicationHostOptions> _applicationHostOptions = null;
        private readonly IScriptEventManager _eventManager = null;
        private readonly ILoggerFactory _loggerFactory = null;
        private readonly IRpcServer _rpcServer = null;
        private readonly ILanguageWorkerProcessFactory _languageWorkerProcessFactory;

        public LanguageWorkerChannelFactory(IScriptEventManager eventManager, IRpcServer rpcServer, ILoggerFactory loggerFactory, IOptions<LanguageWorkerOptions> languageWorkerOptions,
             ILanguageWorkerProcessFactory languageWorkerProcessFactory)
        {
            _rpcServer = rpcServer;
            _eventManager = eventManager;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<WebHostLanguageWorkerChannelManager>();
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _languageWorkerProcessFactory = languageWorkerProcessFactory;
        }

        public ILanguageWorkerChannel CreateLanguageWorkerChannel(string scriptRootPath, string runtime, IMetricsLogger metricsLogger, int attemptCount, bool isWebhostChannel = false, IOptions<ManagedDependencyOptions> managedDependencyOptions = null)
        {
            var languageWorkerConfig = _workerConfigs.Where(c => c.Language.Equals(runtime, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (languageWorkerConfig == null)
            {
                throw new InvalidOperationException($"WorkerCofig for runtime: {runtime} not found");
            }
            string workerId = Guid.NewGuid().ToString();
            ILanguageWorkerProcess languageWorkerProcess = _languageWorkerProcessFactory.CreateLanguageWorkerProcess(workerId, runtime, scriptRootPath);
            return new LanguageWorkerChannel(
                         workerId,
                         scriptRootPath,
                         _eventManager,
                         languageWorkerConfig,
                         languageWorkerProcess,
                         _loggerFactory,
                         metricsLogger,
                         attemptCount,
                         isWebhostChannel,
                         managedDependencyOptions);
        }
    }
}
