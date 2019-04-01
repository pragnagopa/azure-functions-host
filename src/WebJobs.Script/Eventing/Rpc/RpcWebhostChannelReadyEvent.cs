// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Script.Rpc;

namespace Microsoft.Azure.WebJobs.Script.Eventing
{
    /// <summary>
    /// RpcWebHostChannelReadyEvent is published when a language worker channel is started at the
    /// Webhost/Application level. LanguageWorkerChannelManager keeps track that channel created
    /// </summary>
    internal class RpcWebHostChannelReadyEvent : RpcChannelReadyEvent
    {
        internal RpcWebHostChannelReadyEvent(string id, string language, ILanguageWorkerChannel languageWorkerChannel,
            string version, IDictionary<string, string> capabilities)
            : base(id, language, languageWorkerChannel, version, capabilities)
        {
        }
    }
}
