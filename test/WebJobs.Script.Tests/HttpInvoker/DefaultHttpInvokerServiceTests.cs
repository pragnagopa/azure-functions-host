﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.OutOfProc;
using Microsoft.Azure.WebJobs.Script.OutOfProc.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.HttpInvoker
{
    public class DefaultHttpInvokerServiceTests
    {
        private const string TestFunctionName = "testFunctionName";
        private HttpClient _httpClient;
        private DefaultHttpInvokerService _defaultHttpInvokerService;
        private Guid _testInvocationId;
        private HttpInvokerOptions _httpInvokerOptions;
        private ILoggerFactory _testLoggerFactory;
        private int _defaultPort = 8090;
        private TestLogger _testLogger = new TestLogger(TestFunctionName);

        public DefaultHttpInvokerServiceTests()
        {
            _testLoggerFactory = new LoggerFactory();
            _testInvocationId = Guid.NewGuid();
            _httpInvokerOptions = new HttpInvokerOptions()
            {
                Port = _defaultPort
            };
        }

        public static IEnumerable<object[]> TestLogs
        {
            get
            {
                yield return new object[] { new HttpScriptInvocationResult() { Logs = new List<string>() { "test log1", "test log2" } } };
                yield return new object[] { new HttpScriptInvocationResult() };
            }
        }

        [Fact]
        public async Task ProcessDefaultInvocationRequest_Succeeds()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => ValidateRequest(request))
                .ReturnsAsync(GetValidHttpResponseMessage());

            _httpClient = new HttpClient(handlerMock.Object);
            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient, new OptionsWrapper<HttpInvokerOptions>(_httpInvokerOptions), _testLoggerFactory);
            var testScriptInvocationContext = GetScriptInvocationContext();
            await _defaultHttpInvokerService.ProcessDefaultInvocationRequest(testScriptInvocationContext);
            var invocationResult = await testScriptInvocationContext.ResultSource.Task;

            var expectedHttpScriptInvocationResult = GetTestHttpScriptInvocationResult();
            var testLogs = _testLogger.GetLogMessages();
            Assert.True(testLogs.Count() == expectedHttpScriptInvocationResult.Logs.Count());
            Assert.True(testLogs.All(m => m.FormattedMessage.Contains("invocation log")));
            Assert.Equal(expectedHttpScriptInvocationResult.Outputs.Count(), invocationResult.Outputs.Count());
            Assert.Equal(expectedHttpScriptInvocationResult.ReturnValue, invocationResult.Return);
        }

        [Fact]
        public async Task ProcessDefaultInvocationRequest_JsonResponse_Succeeds()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => RequestHandler(request))
                .ReturnsAsync(GetHttpResponseMessageWithJsonContent());

            _httpClient = new HttpClient(handlerMock.Object);
            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient, new OptionsWrapper<HttpInvokerOptions>(_httpInvokerOptions), _testLoggerFactory);
            var testScriptInvocationContext = GetScriptInvocationContext();
            await _defaultHttpInvokerService.ProcessDefaultInvocationRequest(testScriptInvocationContext);
            var invocationResult = await testScriptInvocationContext.ResultSource.Task;
            Assert.Equal(0, invocationResult.Outputs.Count());
            Assert.Null(invocationResult.Return);
        }

        [Fact]
        public async Task ProcessDefaultInvocationRequest_InvalidMediaType_Throws()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => RequestHandler(request))
                .ReturnsAsync(GetHttpResponseMessageWithStringContent());

            _httpClient = new HttpClient(handlerMock.Object);
            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient, new OptionsWrapper<HttpInvokerOptions>(_httpInvokerOptions), _testLoggerFactory);
            var testScriptInvocationContext = GetScriptInvocationContext();
            await _defaultHttpInvokerService.ProcessDefaultInvocationRequest(testScriptInvocationContext);
            await Assert.ThrowsAsync<UnsupportedMediaTypeException>(async () => await testScriptInvocationContext.ResultSource.Task);
        }

        [Fact]
        public async Task ProcessDefaultInvocationRequest_BadRequestResponse_Throws()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => RequestHandler(request))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest
                });

            _httpClient = new HttpClient(handlerMock.Object);
            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient, new OptionsWrapper<HttpInvokerOptions>(_httpInvokerOptions), _testLoggerFactory);
            var testScriptInvocationContext = GetScriptInvocationContext();
            await _defaultHttpInvokerService.ProcessDefaultInvocationRequest(testScriptInvocationContext);
            await Assert.ThrowsAsync<HttpRequestException>(async () => await testScriptInvocationContext.ResultSource.Task);
        }

        [Theory]
        [MemberData(nameof(TestLogs))]
        public void ProcessOutputLogs_Succeeds(HttpScriptInvocationResult httpScriptInvocationResult)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => RequestHandler(request))
                .ReturnsAsync(GetValidHttpResponseMessage());

            _httpClient = new HttpClient(handlerMock.Object);
            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient, new OptionsWrapper<HttpInvokerOptions>(_httpInvokerOptions), _testLoggerFactory);
            _defaultHttpInvokerService.ProcessLogsFromHttpResponse(GetScriptInvocationContext(), httpScriptInvocationResult);
            var testLogs = _testLogger.GetLogMessages();
            Assert.True(testLogs.Count() == httpScriptInvocationResult.Logs?.Count());
            if (httpScriptInvocationResult.Logs != null && httpScriptInvocationResult.Logs.Count() > 1)
            {
                Assert.True(testLogs.All(m => m.FormattedMessage.Contains("test log")));
            }
        }

        public ScriptInvocationContext GetScriptInvocationContext()
        {
            ScriptInvocationContext scriptInvocationContext = new ScriptInvocationContext()
            {
                ExecutionContext = new ExecutionContext()
                {
                    InvocationId = _testInvocationId,
                    FunctionName = TestFunctionName,
                },
                BindingData = GetScriptInvocationBindingData(),
                Inputs = GetScriptInvocationInputs(),
                ResultSource = new TaskCompletionSource<ScriptInvocationResult>(),
                Logger = _testLogger,
                AsyncExecutionContext = System.Threading.ExecutionContext.Capture()
            };
            var functionMetadata = new FunctionMetadata
            {
                Name = TestFunctionName
            };
            var httpTriggerBinding = new BindingMetadata
            {
                Name = "req",
                Type = "httpTrigger",
                Direction = BindingDirection.In,
                Raw = new JObject()
            };
            var queueInputBinding = new BindingMetadata
            {
                Name = "queueInput",
                Type = "queue",
                Direction = BindingDirection.In
            };
            var httpOutputBinding = new BindingMetadata
            {
                Name = "res",
                Type = "http",
                Direction = BindingDirection.Out,
                Raw = new JObject()
            };
            scriptInvocationContext.FunctionMetadata = functionMetadata;
            return scriptInvocationContext;
        }

        private async void ValidateRequest(HttpRequestMessage httpRequestMessage)
        {
            Assert.Contains($"{HttpInvokerConstants.UserAgentHeaderValue}/{ScriptHost.Version}", httpRequestMessage.Headers.UserAgent.ToString());
            Assert.Equal(_testInvocationId.ToString(), httpRequestMessage.Headers.GetValues(HttpInvokerConstants.InvocationIdHeaderName).Single());
            Assert.Equal(ScriptHost.Version, httpRequestMessage.Headers.GetValues(HttpInvokerConstants.HostVersionHeaderName).Single());
            Assert.Equal(httpRequestMessage.RequestUri.ToString(), $"http://localhost:{_defaultPort}/{TestFunctionName}");

            HttpScriptInvocationContext httpScriptInvocationContext = await httpRequestMessage.Content.ReadAsAsync<HttpScriptInvocationContext>();

            // Verify Metadata
            var expectedMetadata = GetScriptInvocationBindingData();
            Assert.Equal(expectedMetadata.Count(), httpScriptInvocationContext.Metadata.Count());
            foreach (var key in expectedMetadata.Keys)
            {
                Assert.Equal(JsonConvert.SerializeObject(expectedMetadata[key]), httpScriptInvocationContext.Metadata[key]);
            }

            // Verify Data
            var expectedData = GetScriptInvocationInputs();
            Assert.Equal(expectedData.Count(), httpScriptInvocationContext.Data.Count());
            foreach (var item in expectedData)
            {
                Assert.True(httpScriptInvocationContext.Data.Keys.Contains(item.name));
                Assert.Equal(JsonConvert.SerializeObject(item.val), httpScriptInvocationContext.Data[item.name]);
            }
        }

        private void RequestHandler(HttpRequestMessage httpRequestMessage)
        {
            //used for tests that do not need request validation
        }

        private List<(string name, DataType type, object val)> GetScriptInvocationInputs()
        {
            List<(string name, DataType type, object val)> inputs = new List<(string name, DataType type, object val)>();
            inputs.Add(("myqueueItem", DataType.String, "HelloWorld"));
            return inputs;
        }

        private Dictionary<string, object> GetScriptInvocationBindingData()
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData["dequeueCount"] = 4;
            bindingData["VisibleTime"] = new DateTime(2019, 10, 1);
            bindingData["helloString"] = "helloMetadata";
            return bindingData;
        }

        private HttpResponseMessage GetValidHttpResponseMessage()
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ObjectContent<HttpScriptInvocationResult>(GetTestHttpScriptInvocationResult(), new JsonMediaTypeFormatter())
            };
        }

        private HttpResponseMessage GetHttpResponseMessageWithStringContent()
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("TestMessage") };
        }

        private HttpResponseMessage GetHttpResponseMessageWithJsonContent()
        {
            JObject response = new JObject();
            response["key1"] = "value1";
            response["key2"] = "value2";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response.ToString(), System.Text.Encoding.UTF8, "application/json") };
        }

        private HttpScriptInvocationResult GetTestHttpScriptInvocationResult()
        {
            return new HttpScriptInvocationResult()
            {
                Logs = new List<string>() { "invocation log1", "invocation log2" },
                Outputs = new Dictionary<string, object>()
                {
                    { "output1", "output1Value" },
                    { "output2", "output2Value" }
                },
                ReturnValue = "Hello return"
            };
        }
    }
}
