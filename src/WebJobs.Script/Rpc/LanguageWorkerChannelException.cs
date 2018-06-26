﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class LanguageWorkerChannelException : Exception
    {
        internal LanguageWorkerChannelException(string message) : base(message)
        {
        }

        internal LanguageWorkerChannelException(string message, Exception innerException) : base(message, innerException)
        {
        }

        internal LanguageWorkerChannelException(LanguageWorkerState languageWorkerChannelState, string message) : base(message, new AggregateException(languageWorkerChannelState.Errors.ToList()))
        {
            LanguageWorkerChannelState = languageWorkerChannelState;
        }

        internal LanguageWorkerState LanguageWorkerChannelState { get; private set; }
    }
}
