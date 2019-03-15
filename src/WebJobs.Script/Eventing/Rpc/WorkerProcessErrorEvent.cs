// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Script.Eventing
{
    public class WorkerProcessErrorEvent : ScriptEvent
    {
        internal WorkerProcessErrorEvent(string language, Exception exception) : base(nameof(WorkerProcessErrorEvent), EventSources.WorkerProcess)
        {
            Exception = exception;
            Language = language;
        }

        internal string Language { get; private set; }

        public Exception Exception { get; private set; }
    }
}
