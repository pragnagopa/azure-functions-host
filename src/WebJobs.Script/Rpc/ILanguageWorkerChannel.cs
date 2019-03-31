﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs.Script.Description;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public delegate ILanguageWorkerChannel CreateChannel(string language, IObservable<FunctionMetadata> registrations, int attemptCount);

    public interface ILanguageWorkerChannel : IDisposable
    {
        string Id { get; }

        WorkerConfig Config { get; }

        void RegisterFunctions(IObservable<FunctionMetadata> functionRegistrations);

        void SendFunctionEnvironmentReloadRequest();

        void SendInvocationRequest(ScriptInvocationContext invocationContext);

        void StartWorkerProcess();
    }
}
