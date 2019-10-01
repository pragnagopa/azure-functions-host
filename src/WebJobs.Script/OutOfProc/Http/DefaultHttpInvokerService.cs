// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.WebApiCompatShim;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc.Http
{
    public class DefaultHttpInvokerService : IHttpInvokerService
    {
        private readonly HttpClient _httpClient;
        private readonly HttpInvokerOptions _httpInvokerOptions;
        private readonly string _httpInvokerBaseUrl;

        public DefaultHttpInvokerService(HttpClient httpClient, IOptions<HttpInvokerOptions> httpInvokerOptions)
        {
            _httpClient = httpClient;
            _httpInvokerOptions = httpInvokerOptions.Value;
            _httpInvokerBaseUrl = $"http://localhost:{_httpInvokerOptions.Port}/";
        }

        public async Task InvokeAsync(ScriptInvocationContext scriptInvocationContext)
        {
            if (Utility.IsSimpleHttpTriggerFunction(scriptInvocationContext.FunctionMetadata))
            {
                await ProcessSimpleHttpInvocationRequest(scriptInvocationContext);
                return;
            }
            await ProcessDefaultInvocationRequest(scriptInvocationContext);
        }

        private async Task ProcessSimpleHttpInvocationRequest(ScriptInvocationContext scriptInvocationContext)
        {
            var functionInvocationUri = _httpInvokerBaseUrl + scriptInvocationContext.FunctionMetadata.Name;
            HttpRequestMessage httpRequestMessage = null;
            var input = scriptInvocationContext.Inputs.FirstOrDefault();
            HttpRequest httpRequest = (HttpRequest)input.val;
            if (httpRequest == null)
            {
                throw new ArgumentNullException(nameof(input.val));
            }
            try
            {
                // populate input bindings
                HttpRequestMessageFeature hreqmf = new HttpRequestMessageFeature(httpRequest.HttpContext);
                httpRequestMessage = hreqmf.HttpRequestMessage;
                string httpInvokerUri = QueryHelpers.AddQueryString(functionInvocationUri, HttpRequestMessageConverters.ConvertQueryCollectionToDictionary(httpRequest.Query));
                httpRequestMessage.RequestUri = new Uri(httpInvokerUri);

                // TODO Add standard headers
                httpRequestMessage.Headers.Add(HttpInvokerConstants.InvocationIdHeaderName, scriptInvocationContext.ExecutionContext.InvocationId.ToString());
                httpRequestMessage.Headers.Add(HttpInvokerConstants.HostVersionHeaderName, ScriptHost.Version);
                // TODO: user agent or X-header?
                httpRequestMessage.Headers.UserAgent.ParseAdd($"{HttpInvokerConstants.UserAgentHeaderValue}/{ScriptHost.Version}");

                HttpResponseMessage invocationResponse = await _httpClient.SendAsync(httpRequestMessage);
                ScriptInvocationResult scriptInvocationResult = new ScriptInvocationResult()
                {
                    Outputs = new Dictionary<string, object>(),
                    Return = new object()
                };
                BindingMetadata httpOutputBinding = scriptInvocationContext.FunctionMetadata.OutputBindings.FirstOrDefault();
                if (httpOutputBinding != null)
                {
                    scriptInvocationResult.Outputs.Add(httpOutputBinding.Name, invocationResponse);
                    // TODO set reuturn always?
                    scriptInvocationResult.Return = invocationResponse;
                }
                scriptInvocationContext.ResultSource.SetResult(scriptInvocationResult);
            }
            catch (Exception responseEx)
            {
                scriptInvocationContext.ResultSource.TrySetException(responseEx);
            }
        }

        private async Task ProcessDefaultInvocationRequest(ScriptInvocationContext scriptInvocationContext)
        {
            var functionInvocationUri = _httpInvokerBaseUrl + scriptInvocationContext.FunctionMetadata.Name;
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
                HttpResponseMessage response = await _httpClient.PostAsync(functionInvocationUri, content);
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
