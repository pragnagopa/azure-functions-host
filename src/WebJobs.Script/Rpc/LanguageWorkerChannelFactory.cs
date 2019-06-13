﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.ManagedDependencies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class LanguageWorkerChannelFactory : ILanguageWorkerChannelFactory
    {
        private readonly ILoggerFactory _loggerFactory = null;
        private readonly ILanguageWorkerProcessFactory _languageWorkerProcessManager = null;
        private readonly IScriptEventManager _eventManager = null;
        private readonly IEnumerable<WorkerConfig> _workerConfigs = null;

        public LanguageWorkerChannelFactory(IScriptEventManager eventManager, IEnvironment environment, IRpcServer rpcServer, ILoggerFactory loggerFactory, IOptions<LanguageWorkerOptions> languageWorkerOptions,
            IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions, ILanguageWorkerProcessFactory languageWorkerProcessManager)
        {
            _eventManager = eventManager;
            _loggerFactory = loggerFactory;
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _languageWorkerProcessManager = languageWorkerProcessManager;
        }

        public ILanguageWorkerChannel CreateLanguageWorkerChannel(string workerId, string scriptRootPath, string language, IMetricsLogger metricsLogger, int attemptCount, bool isWebhostChannel = false, IOptions<ManagedDependencyOptions> managedDependencyOptions = null)
        {
            var languageWorkerConfig = _workerConfigs.Where(c => c.Language.Equals(language, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (languageWorkerConfig == null)
            {
                throw new InvalidOperationException($"WorkerCofig for runtime: {language} not found");
            }
            ILanguageWorkerProcess languageWorkerProcess = _languageWorkerProcessManager.CreateLanguageWorkerProcess(workerId, language, scriptRootPath);
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
