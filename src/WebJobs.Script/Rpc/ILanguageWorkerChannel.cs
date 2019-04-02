// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Script.Description;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public interface ILanguageWorkerChannel : IDisposable
    {
        string Id { get; }

        WorkerConfig Config { get; }

        void RegisterFunctions(IObservable<FunctionMetadata> functionRegistrations);

        void SendFunctionEnvironmentReloadRequest();

        void SendInvocationRequest(ScriptInvocationContext context);

        void StartWorkerProcess();
    }
}
