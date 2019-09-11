// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Extensibility;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Description
{
    internal class RpcFunctionDescriptorProvider : OutOfProcDescriptorProviderBase
    {
        private readonly ILoggerFactory _loggerFactory;
        private IFunctionDispatcher _dispatcher;
        private string _workerRuntime;

        public RpcFunctionDescriptorProvider(ScriptHost host, string workerRuntime, ScriptJobHostOptions config, ICollection<IScriptBindingProvider> bindingProviders,
            IFunctionDispatcher dispatcher, ILoggerFactory loggerFactory)
            : base(host, config, bindingProviders, dispatcher, loggerFactory)
        {
            _dispatcher = dispatcher;
            _loggerFactory = loggerFactory;
            _workerRuntime = workerRuntime;
        }

        public override async Task<(bool, FunctionDescriptor)> TryCreate(FunctionMetadata functionMetadata)
        {
            if (functionMetadata == null)
            {
                throw new ArgumentNullException(nameof(functionMetadata));
            }

            if (!_dispatcher.IsSupported(functionMetadata, _workerRuntime))
            {
                return (false, null);
            }

            return await base.TryCreate(functionMetadata);
        }
    }
}
