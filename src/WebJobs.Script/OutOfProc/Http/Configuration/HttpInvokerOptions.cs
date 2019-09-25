// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Script.Abstractions;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc.Http
{
    public class HttpInvokerOptions
    {
        public WorkerDescription Description { get; set; }

        internal WorkerProcessArguments Arguments { get; set; }
    }
}
