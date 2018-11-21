// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Abstractions;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public interface ILanguageWorkerService : IDisposable
    {
        IDictionary<string, ILanguageWorkerChannel> LanguageWorkerChannels { get; }

        Task InitializeLanguageWorkerChannelsAsync();
    }
}
