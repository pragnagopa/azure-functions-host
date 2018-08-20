// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class LanguageWorkerService : ILanguageWorkerService
    {
        private IEnumerable<WorkerConfig> _languageWorkerConfigs;

        internal LanguageWorkerService(IConfiguration rootConfig)
        {
            var languageWorkerSection = rootConfig.GetSection(LanguageWorkerConstants.LanguageWorkersSectionName);
            var configFactory = new WorkerConfigFactory(languageWorkerSection, null);
            var providers = new List<IWorkerProvider>();
            providers.AddRange(configFactory.GetWorkerProviders(NullLogger.Instance));
            _languageWorkerConfigs = configFactory.GetConfigs(providers);
        }

        public IEnumerable<WorkerConfig> LanguageWorkerConfigs => _languageWorkerConfigs;
    }
}
