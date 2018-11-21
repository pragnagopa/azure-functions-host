// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public interface IPlaceHolderLanguageWorkerService : IDisposable
    {
        IDictionary<string, ILanguageWorkerChannel> PlaceHolderChannels { get; }

        Task InitializePlaceHolderChannelsAsync();
    }
}
