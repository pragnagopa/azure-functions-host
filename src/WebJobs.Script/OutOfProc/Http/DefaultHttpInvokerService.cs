// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc.Http
{
    public class DefaultHttpInvokerService : IHttpInvokerService
    {
        private readonly HttpClient _httpClient;
        private readonly string _httpInvokerBaseUrl;

        public DefaultHttpInvokerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // TODO: user agent ok?
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(HttpInvokerConstants.UserAgentHeaderValue);
            _httpInvokerBaseUrl = "http://localhost:8090/";
        }

        public async Task InvokeAsync(ScriptInvocationContext scriptInvocationContext)
        {
            var requestUri = _httpInvokerBaseUrl + scriptInvocationContext.FunctionMetadata.Name;
            HttpScriptInvocationContext httpScriptInvocationContext = new HttpScriptInvocationContext();

            // populate metadata
            foreach (var bindingDataPair in scriptInvocationContext.BindingData)
            {
                if (bindingDataPair.Value != null)
                {
                    if (bindingDataPair.Value is HttpRequest)
                    {
                        // no metadata for httpTrigger
                        continue;
                    }
                    // TODO: how to handle byte[]? base64 encoded string?
                    httpScriptInvocationContext.Metadata[bindingDataPair.Key] = JsonConvert.SerializeObject(bindingDataPair.Value);
                }
            }

            // populate input bindings
            foreach (var input in scriptInvocationContext.Inputs)
            {
                if (input.val is HttpRequest httpRequest)
                {
                    httpScriptInvocationContext.Data[input.name] = HttpRequestMessageConverters.ConvertHttpRequestToJObject(httpRequest);
                    continue;
                }
                httpScriptInvocationContext.Data[input.name] = JsonConvert.SerializeObject(input.val);
            }
            HttpContent content = new ObjectContent<HttpScriptInvocationContext>(httpScriptInvocationContext, new JsonMediaTypeFormatter());
            // Add standard headers
            content.Headers.Add(HttpInvokerConstants.InvocationIdHeaderName, scriptInvocationContext.ExecutionContext.InvocationId.ToString());
            content.Headers.Add(HttpInvokerConstants.HostVersionHeaderName, ScriptHost.Version);
            try
            {
                HttpResponseMessage response = await _httpClient.PostAsync(requestUri, content);
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
