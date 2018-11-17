﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Script.Description;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public interface IFunctionDispatcher : IDisposable
    {
        IDictionary<string, LanguageWorkerState> LanguageWorkerChannelStates { get; }

        // Tests if the function metadata is supported by a known language worker
        bool IsSupported(FunctionMetadata metadata);

        // Registers a supported function with the dispatcher
        void Register(FunctionRegistrationContext context);

        void RegisterFunctions();

        LanguageWorkerState CreateWorkerStateWithExistingChannel(string language, ILanguageWorkerChannel languageWorkerChannel);

        LanguageWorkerState CreateWorkerState(string language);
    }
}
