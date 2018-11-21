// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public interface IJobHostLanguageWorkerService : IDisposable
    {
        IDictionary<string, ILanguageWorkerChannel> JobHostChannels { get; }

        Task InitializeJobHostChannelAsync();
    }
}
