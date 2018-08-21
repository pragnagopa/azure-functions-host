// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class LanguageWorkerService : ILanguageWorkerService, IConfigureOptions<ScriptJobHostOptions>
    {
        private IEnumerable<WorkerConfig> _languageWorkerConfigs;

        internal LanguageWorkerService(IConfiguration rootConfig)
        {
            ILoggerFactory loggerFactory = NullLoggerFactory.Instance;

            var configFactory = new WorkerConfigFactory(rootConfig, loggerFactory.CreateLogger("Host.LanguageWorkerService"));
            var providers = new List<IWorkerProvider>();
            providers.AddRange(configFactory.GetWorkerProviders(NullLogger.Instance));
            _languageWorkerConfigs = configFactory.GetConfigs(providers);
        }

        public IEnumerable<WorkerConfig> LanguageWorkerConfigs => _languageWorkerConfigs;

        public void Configure(ScriptJobHostOptions options)
        {
            throw new System.NotImplementedException();
        }
    }
}
