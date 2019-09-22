// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Description;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    public class DefaultHttpInvokerService : IHttpInvokerService
    {
        private readonly HttpClient _httpClient;
        private readonly string _httpInvokerBaseUrl;

        public DefaultHttpInvokerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpInvokerBaseUrl = "http://localhost:8090";
        }

        public async Task GetInvocationResponse(ScriptInvocationContext scriptInvocationContext)
        {
            var requestUri = _httpInvokerBaseUrl + scriptInvocationContext.FunctionMetadata.Name;
            // TODO: convert send ScriptInvocationContext to http request and send to http invoker
            HttpContent content = new StringContent("hello");
            await _httpClient.PostAsync(requestUri, content);
            throw new NotImplementedException();
        }
    }
}
