// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Script.OutOfProc.Http
{
    public class HttpInvokerOptions
    {
        public HttpWorkerDescription Description { get; set; }

        internal WorkerProcessArguments Arguments { get; set; }

        internal int Port { get; set; }
    }
}
