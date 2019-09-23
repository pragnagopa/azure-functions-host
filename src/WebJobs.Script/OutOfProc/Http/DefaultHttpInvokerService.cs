﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Script.Description;
using Newtonsoft.Json.Linq;

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
            UriBuilder baseUri = new UriBuilder(requestUri);
            // TODO: convert send ScriptInvocationContext to http request and send to http invoker
            HttpScriptInvocationContext httpScriptInvocationContext = new HttpScriptInvocationContext();
            HttpContent content = new ObjectContent<HttpScriptInvocationContext>(httpScriptInvocationContext, new JsonMediaTypeFormatter());

            // Add standard headers
            content.Headers.Add(HttpInvokerConstants.InvocatoinIdHeaderName, scriptInvocationContext.ExecutionContext.InvocationId.ToString());
            content.Headers.Add(HttpInvokerConstants.HostVersionHeader, ScriptHost.Version);

            // populate metadata
            foreach (var bindingDataPair in scriptInvocationContext.BindingData)
            {
                if (bindingDataPair.Value != null)
                {
                    if (bindingDataPair.Value is HttpRequest)
                    {
                        continue;
                    }
                    httpScriptInvocationContext.Metadata[bindingDataPair.Key] = MessageConversionUtilities.ToJsonString(bindingDataPair.Value);
                }
            }

            // populate input bindings
            foreach (var input in scriptInvocationContext.Inputs)
            {
                httpScriptInvocationContext.Data[input.name] = input.val.ToJObject();
            }
            await _httpClient.PostAsync(requestUri, content);
        }
    }
}
