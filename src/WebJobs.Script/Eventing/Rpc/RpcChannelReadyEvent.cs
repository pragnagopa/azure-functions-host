// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Script.Rpc;

namespace Microsoft.Azure.WebJobs.Script.Eventing
{
    public class RpcChannelReadyEvent : ScriptEvent
    {
        internal RpcChannelReadyEvent(string language, ILanguageWorkerChannel languageWorkerChannel)
            : base(nameof(RpcChannelReadyEvent), EventSources.Rpc)
        {
            LanguageWorkerChannel = languageWorkerChannel;
            Language = language;
        }

        public ILanguageWorkerChannel LanguageWorkerChannel { get; }

        public string Language { get; }
    }
}
