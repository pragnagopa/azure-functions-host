// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.ManagedDependencies;
using Microsoft.Extensions.Options;
using FunctionMetadata = Microsoft.Azure.WebJobs.Script.Description.FunctionMetadata;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public interface ILanguageWorkerChannelManager
    {
        Task<ILanguageWorkerChannel> InitializeChannelAsync(string language);

        ILanguageWorkerChannel GetChannel(string language);

        IEnumerable<ILanguageWorkerChannel> GetChannels(string language);

        Task SpecializeAsync();

        bool ShutdownChannelsIfExist(string language);

        void ShutdownStandbyChannels(IEnumerable<FunctionMetadata> functions);

        void ShutdownChannels();

        ILanguageWorkerChannel CreateLanguageWorkerChannel(string workerId, string scriptRootPath, string language, IMetricsLogger metricsLogger, int attemptCount, bool isWebhostChannel = false, IOptions<ManagedDependencyOptions> managedDependencyOptions = null);
    }
}
