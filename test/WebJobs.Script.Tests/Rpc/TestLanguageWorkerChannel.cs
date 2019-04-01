// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Rpc;

namespace Microsoft.Azure.WebJobs.Script.Tests.Rpc
{
    public class TestLanguageWorkerChannel : ILanguageWorkerChannel
    {
        private string _workerId;

        public TestLanguageWorkerChannel(string workerId)
        {
            _workerId = workerId;
        }

        public string Id => _workerId;

        public WorkerConfig Config => throw new NotImplementedException();

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void RegisterFunctions(IObservable<FunctionMetadata> functionRegistrations)
        {
            throw new NotImplementedException();
        }

        public void SendFunctionEnvironmentReloadRequest()
        {
            throw new NotImplementedException();
        }

        public void SendInvocationRequest(ScriptInvocationContext context)
        {
            throw new NotImplementedException();
        }

        public void StartWorkerProcess()
        {
            throw new NotImplementedException();
        }
    }
}
