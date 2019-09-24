﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Extensions.Logging;
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
            _httpInvokerBaseUrl = "http://localhost:8090/";
        }

        public async Task GetInvocationResponse(ScriptInvocationContext scriptInvocationContext)
        {
            var requestUri = _httpInvokerBaseUrl + scriptInvocationContext.FunctionMetadata.Name;
            // TODO: convert send ScriptInvocationContext to http request and send to http invoker
            HttpScriptInvocationContext httpScriptInvocationContext = new HttpScriptInvocationContext();
            httpScriptInvocationContext.Metadata = new JObject();
            httpScriptInvocationContext.Data = new JObject();
            // populate metadata
            foreach (var bindingDataPair in scriptInvocationContext.BindingData)
            {
                if (bindingDataPair.Value != null)
                {
                    if (bindingDataPair.Value is HttpRequest)
                    {
                        continue;
                    }
                    httpScriptInvocationContext.Metadata[bindingDataPair.Key] = bindingDataPair.Value.ToJToken();
                }
            }

            // populate input bindings
            foreach (var input in scriptInvocationContext.Inputs)
            {
                httpScriptInvocationContext.Data[input.name] = input.val.ToJToken();
            }
            HttpContent content = new ObjectContent<HttpScriptInvocationContext>(httpScriptInvocationContext, new JsonMediaTypeFormatter());
            // Add standard headers
            // content.Headers.Add(HttpInvokerConstants.InvocatoinIdHeaderName, scriptInvocationContext.ExecutionContext.InvocationId.ToString());
            //content.Headers.Add(HttpInvokerConstants.HostVersionHeader, ScriptHost.Version);
            try
            {
                HttpResponseMessage response = await _httpClient.PostAsync(requestUri, content);
                //string invocationResultString = await response.Content.ReadAsStringAsync();
                HttpScriptInvocationResult invocationResult = await response.Content.ReadAsAsync<HttpScriptInvocationResult>();
                ProcessLogsFromHttpResponse(scriptInvocationContext, invocationResult);
                var result = new ScriptInvocationResult()
                {
                    Outputs = invocationResult.Outputs,
                    Return = invocationResult?.ReturnValue
                };
                scriptInvocationContext.ResultSource.SetResult(result);
            }
            catch (Exception responseEx)
            {
                scriptInvocationContext.ResultSource.TrySetException(responseEx);
            }
        }

        internal void ProcessLogsFromHttpResponse(ScriptInvocationContext scriptInvocationContext, HttpScriptInvocationResult invocationResult)
        {
            // Restore the execution context from the original invocation. This allows AsyncLocal state to flow to loggers.
            System.Threading.ExecutionContext.Run(scriptInvocationContext.AsyncExecutionContext, (s) =>
            {
                foreach (var userLog in invocationResult.Logs)
                {
                    scriptInvocationContext.Logger.LogInformation(userLog);
                }
            }, null);
        }
    }
}
