﻿// Copyright (c) .NET Foundation. All rights reserved.
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

        public Task InvokeAsync(ScriptInvocationContext scriptInvocationContext)
        {
            if (Utility.IsSimpleHttpTriggerFunction(scriptInvocationContext.FunctionMetadata))
            {
                return ProcessSimpleHttpInvocationRequest(scriptInvocationContext);
            }
            return ProcessDefaultInvocationRequest(scriptInvocationContext);
        }

        internal async Task ProcessSimpleHttpInvocationRequest(ScriptInvocationContext scriptInvocationContext)
        {
            ScriptInvocationResult scriptInvocationResult = new ScriptInvocationResult()
            {
                Outputs = new Dictionary<string, object>(),
                Return = new object()
            };
            HttpRequestMessage httpRequestMessage = null;
            var input = scriptInvocationContext.Inputs.FirstOrDefault();
            HttpRequest httpRequest = (HttpRequest)input.val;
            if (httpRequest == null)
            {
                throw new ArgumentNullException(nameof(httpRequest));
            }
            try
            {
                // Build HttpRequestMessage from HttpTrigger binding
                HttpRequestMessageFeature httpRequestMessageFeature = new HttpRequestMessageFeature(httpRequest.HttpContext);
                httpRequestMessage = httpRequestMessageFeature.HttpRequestMessage;

                AddRequestHeadersAndSetRequestUri(httpRequestMessage, scriptInvocationContext.FunctionMetadata.Name, scriptInvocationContext.ExecutionContext.InvocationId.ToString());

                // Populate query params from httpTrigger
                string httpInvokerUri = QueryHelpers.AddQueryString(httpRequestMessage.RequestUri.ToString(), HttpRequestMessageConverters.ConvertQueryCollectionToDictionary(httpRequest.Query));
                httpRequestMessage.RequestUri = new Uri(httpInvokerUri);

                HttpResponseMessage invocationResponse = await _httpClient.SendAsync(httpRequestMessage);

                BindingMetadata httpOutputBinding = scriptInvocationContext.FunctionMetadata.OutputBindings.FirstOrDefault();
                if (httpOutputBinding != null)
                {
                    // handle http output binding
                    scriptInvocationResult.Outputs.Add(httpOutputBinding.Name, invocationResponse);
                    // handle $return
                    scriptInvocationResult.Return = invocationResponse;
                }
                scriptInvocationContext.ResultSource.SetResult(scriptInvocationResult);
            }
            catch (Exception responseEx)
            {
                scriptInvocationContext.ResultSource.TrySetException(responseEx);
            }
        }

        internal async Task ProcessDefaultInvocationRequest(ScriptInvocationContext scriptInvocationContext)
        {
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

            // build HttpContent from ScriptInvocationContext
            HttpContent content = new ObjectContent<HttpScriptInvocationContext>(httpScriptInvocationContext, new JsonMediaTypeFormatter());

            try
            {
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage();
                AddRequestHeadersAndSetRequestUri(httpRequestMessage, scriptInvocationContext.FunctionMetadata.Name, scriptInvocationContext.ExecutionContext.InvocationId.ToString());
                httpRequestMessage.Content = content;
                HttpResponseMessage response = await _httpClient.SendAsync(httpRequestMessage);
                HttpScriptInvocationResult invocationResult = await response.Content.ReadAsAsync<HttpScriptInvocationResult>();
                ScriptInvocationResult scriptInvocationResult = new ScriptInvocationResult();
                if (invocationResult != null)
                {
                    ProcessLogsFromHttpResponse(scriptInvocationContext, invocationResult);
                    scriptInvocationResult.Outputs = invocationResult.Outputs;
                    scriptInvocationResult.Return = invocationResult?.ReturnValue;
                }
                scriptInvocationContext.ResultSource.SetResult(scriptInvocationResult);
            }
            catch (Exception responseEx)
            {
                scriptInvocationContext.ResultSource.TrySetException(responseEx);
            }
        }

        internal void ProcessLogsFromHttpResponse(ScriptInvocationContext scriptInvocationContext, HttpScriptInvocationResult invocationResult)
        {
            if (scriptInvocationContext == null)
            {
                throw ArgumentNullException(nameof(scriptInvocationContext));
            }
            if (invocationResult == null)
            {
                throw ArgumentNullException(nameof(invocationResult));
            }
            if (invocationResult.Logs != null)
            {
                // Restore the execution context from the original invocation. This allows AsyncLocal state to flow to loggers.
                System.Threading.ExecutionContext.Run(scriptInvocationContext.AsyncExecutionContext, (s) =>
                {
                    foreach (var userLog in invocationResult.Logs)
                    {
                        scriptInvocationContext.Logger?.LogInformation(userLog);
                    }
                }, null);
            }
        }

        private Exception ArgumentNullException(string v)
        {
            throw new NotImplementedException();
        }

        private void AddRequestHeadersAndSetRequestUri(HttpRequestMessage httpRequestMessage, string functionName, string invocationId)
        {
            httpRequestMessage.RequestUri = new Uri(new UriBuilder("http", "localhost", _httpInvokerOptions.Port, functionName).ToString());
            // TODO any other standard headers? DateTime?
            httpRequestMessage.Headers.Add(HttpInvokerConstants.InvocationIdHeaderName, invocationId);
            httpRequestMessage.Headers.Add(HttpInvokerConstants.HostVersionHeaderName, ScriptHost.Version);
            // TODO: user agent or X-header?
            httpRequestMessage.Headers.UserAgent.ParseAdd($"{HttpInvokerConstants.UserAgentHeaderValue}/{ScriptHost.Version}");
        }
    }
}
