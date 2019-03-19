// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public enum LanguageWorkerChannelState
    {
        /// <summary>
        /// The host has been fully initialized and can accept direct function
        /// invocations. All functions have been indexed. Listeners may not yet
        /// be not yet running.
        /// </summary>
        Initialized
    }
}
