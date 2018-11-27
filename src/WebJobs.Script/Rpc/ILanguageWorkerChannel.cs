// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Eventing.Rpc;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public delegate ILanguageWorkerChannel CreateChannel(WorkerConfig conf, IObservable<FunctionRegistrationContext> registrations, int attemptCount);

    public interface ILanguageWorkerChannel : IDisposable
    {
        string Id { get; }

        WorkerConfig Config { get; }

        bool IsPlaceHolderChannel { get;  }

        void RegisterFunctions(IObservable<FunctionRegistrationContext> functionRegistrations);

        void SendFunctionEnvironmentRequest();

        void CreateWorkerContextAndStartProcess();
    }
}
