// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.ManagedDependencies;
using Microsoft.Azure.WebJobs.Script.OutOfProc.Http;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    public interface IFunctionDispatcherFactory
    {
        IFunctionDispatcher GetFunctionDispatcher();
    }
}
