// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Script.Extensibility;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Description
{
    internal class HttpFunctionDescriptorProvider : OutOfProcDescriptorProviderBase
    {
        private readonly ILoggerFactory _loggerFactory;
        private IFunctionDispatcher _dispatcher;

        public HttpFunctionDescriptorProvider(ScriptHost host, ScriptJobHostOptions config, ICollection<IScriptBindingProvider> bindingProviders,
            IFunctionDispatcher dispatcher, ILoggerFactory loggerFactory)
            : base(host, config, bindingProviders, dispatcher, loggerFactory)
        {
            _dispatcher = dispatcher;
            _loggerFactory = loggerFactory;
        }
    }
}
