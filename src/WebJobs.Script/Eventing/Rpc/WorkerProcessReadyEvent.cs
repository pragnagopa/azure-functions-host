// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Script.Eventing
{
    public class WorkerProcessReadyEvent : ScriptEvent
    {
        internal WorkerProcessReadyEvent(string language)
            : base(nameof(WorkerProcessReadyEvent), EventSources.Worker)
        {
            Language = language ?? throw new ArgumentNullException(nameof(language));
        }

        internal string Language { get; private set; }
    }
}
