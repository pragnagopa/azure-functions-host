// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Script.Rpc;

namespace Microsoft.Azure.WebJobs.Script.Eventing
{
    internal class RpcFunctionLoadedEvent : RpcChannelEvent
    {
        internal RpcFunctionLoadedEvent(string id)
            : base(id)
        {
        }
    }
}
