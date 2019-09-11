// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Script.Eventing
{
    public class HttpWorkerErrorEvent : ScriptEvent
    {
        internal HttpWorkerErrorEvent(string workerId, Exception exception)
            : base(nameof(HttpWorkerErrorEvent), EventSources.Rpc)
        {
            WorkerId = workerId;
            Exception = exception;
        }

        public string WorkerId { get; }

        public Exception Exception { get; private set; }
    }
}
