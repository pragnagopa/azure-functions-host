﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Diagnostics;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public interface ILanguageWorkerChannelManager
    {
        IDictionary<string, ILanguageWorkerChannel> InitializedChannels { get; set; }

        Task InitializePlaceHolderChannelsAsync(IEnumerable<string> languages);

        void InitializeMetricsLogger(IMetricsLogger metricsLogger);

        ILanguageWorkerChannel CreateLanguageWorkerChannel(string scriptRootPath, string language, bool isJobHostChannel, int attemptCount);
    }
}
