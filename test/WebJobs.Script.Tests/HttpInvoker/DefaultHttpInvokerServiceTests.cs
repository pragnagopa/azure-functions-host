// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.OutOfProc;
using Microsoft.Azure.WebJobs.Script.OutOfProc.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
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
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => ValidateDefaultInvocationRequest(request))
                .ReturnsAsync(HttpInvokerTestUtilities.GetValidHttpResponseMessage());

            _httpClient = new HttpClient(handlerMock.Object);
            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient, new OptionsWrapper<HttpInvokerOptions>(_httpInvokerOptions), _testLoggerFactory);
            var testScriptInvocationContext = HttpInvokerTestUtilities.GetScriptInvocationContext(TestFunctionName, _testInvocationId, _testLogger);
            await _defaultHttpInvokerService.ProcessDefaultInvocationRequest(testScriptInvocationContext);
            var invocationResult = await testScriptInvocationContext.ResultSource.Task;

            var expectedHttpScriptInvocationResult = HttpInvokerTestUtilities.GetTestHttpScriptInvocationResult();
            var testLogs = _testLogger.GetLogMessages();
            Assert.True(testLogs.Count() == expectedHttpScriptInvocationResult.Logs.Count());
            Assert.True(testLogs.All(m => m.FormattedMessage.Contains("invocation log")));
            Assert.Equal(expectedHttpScriptInvocationResult.Outputs.Count(), invocationResult.Outputs.Count());
            Assert.Equal(expectedHttpScriptInvocationResult.ReturnValue, invocationResult.Return);
        }

        [Fact]
        public async Task ProcessSimpleHttpTriggerInvocationRequest_Succeeds()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => ValidateSimpleHttpTriggerInvocationRequest(request))
                .ReturnsAsync(HttpInvokerTestUtilities.GetValidSimpleHttpResponseMessage());

            _httpClient = new HttpClient(handlerMock.Object);
            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient, new OptionsWrapper<HttpInvokerOptions>(_httpInvokerOptions), _testLoggerFactory);
            var testScriptInvocationContext = HttpInvokerTestUtilities.GetSimpleHttpTriggerScriptInvocationContext(TestFunctionName, _testInvocationId, _testLogger);
            await _defaultHttpInvokerService.ProcessSimpleHttpInvocationRequest(testScriptInvocationContext);
            var invocationResult = await testScriptInvocationContext.ResultSource.Task;
            var expectedHttpResponseMessage = HttpInvokerTestUtilities.GetValidSimpleHttpResponseMessage();
            var expectedResponseContent = await expectedHttpResponseMessage.Content.ReadAsStringAsync();
            var testLogs = _testLogger.GetLogMessages();
            Assert.Equal(0, testLogs.Count());
            if (invocationResult.Return is HttpResponseMessage response)
            {
                Assert.Equal(expectedHttpResponseMessage.StatusCode, response.StatusCode);
                Assert.Equal(expectedResponseContent, await response.Content.ReadAsStringAsync());
            }
        }

        [Fact]
        public async Task ProcessDefaultInvocationRequest_JsonResponse_Succeeds()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => RequestHandler(request))
                .ReturnsAsync(HttpInvokerTestUtilities.GetHttpResponseMessageWithJsonContent());

            _httpClient = new HttpClient(handlerMock.Object);
            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient, new OptionsWrapper<HttpInvokerOptions>(_httpInvokerOptions), _testLoggerFactory);
            var testScriptInvocationContext = HttpInvokerTestUtilities.GetScriptInvocationContext(TestFunctionName, _testInvocationId, _testLogger);
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
                .ReturnsAsync(HttpInvokerTestUtilities.GetHttpResponseMessageWithStringContent());

            _httpClient = new HttpClient(handlerMock.Object);
            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient, new OptionsWrapper<HttpInvokerOptions>(_httpInvokerOptions), _testLoggerFactory);
            var testScriptInvocationContext = HttpInvokerTestUtilities.GetScriptInvocationContext(TestFunctionName, _testInvocationId, _testLogger);
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
            var testScriptInvocationContext = HttpInvokerTestUtilities.GetScriptInvocationContext(TestFunctionName, _testInvocationId, _testLogger);
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
                .ReturnsAsync(HttpInvokerTestUtilities.GetValidHttpResponseMessage());

            _httpClient = new HttpClient(handlerMock.Object);
            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient, new OptionsWrapper<HttpInvokerOptions>(_httpInvokerOptions), _testLoggerFactory);
            _defaultHttpInvokerService.ProcessLogsFromHttpResponse(HttpInvokerTestUtilities.GetScriptInvocationContext(TestFunctionName, _testInvocationId, _testLogger), httpScriptInvocationResult);
            var testLogs = _testLogger.GetLogMessages();
            Assert.True(testLogs.Count() == httpScriptInvocationResult.Logs?.Count());
            if (httpScriptInvocationResult.Logs != null && httpScriptInvocationResult.Logs.Count() > 1)
            {
                Assert.True(testLogs.All(m => m.FormattedMessage.Contains("test log")));
            }
        }

        private async void ValidateDefaultInvocationRequest(HttpRequestMessage httpRequestMessage)
        {
            Assert.Contains($"{HttpInvokerConstants.UserAgentHeaderValue}/{ScriptHost.Version}", httpRequestMessage.Headers.UserAgent.ToString());
            Assert.Equal(_testInvocationId.ToString(), httpRequestMessage.Headers.GetValues(HttpInvokerConstants.InvocationIdHeaderName).Single());
            Assert.Equal(ScriptHost.Version, httpRequestMessage.Headers.GetValues(HttpInvokerConstants.HostVersionHeaderName).Single());
            Assert.Equal(httpRequestMessage.RequestUri.ToString(), $"http://localhost:{_defaultPort}/{TestFunctionName}");

            HttpScriptInvocationContext httpScriptInvocationContext = await httpRequestMessage.Content.ReadAsAsync<HttpScriptInvocationContext>();

            // Verify Metadata
            var expectedMetadata = HttpInvokerTestUtilities.GetScriptInvocationBindingData();
            Assert.Equal(expectedMetadata.Count(), httpScriptInvocationContext.Metadata.Count());
            foreach (var key in expectedMetadata.Keys)
            {
                Assert.Equal(JsonConvert.SerializeObject(expectedMetadata[key]), httpScriptInvocationContext.Metadata[key]);
            }

            // Verify Data
            var expectedData = HttpInvokerTestUtilities.GetScriptInvocationInputs();
            Assert.Equal(expectedData.Count(), httpScriptInvocationContext.Data.Count());
            foreach (var item in expectedData)
            {
                Assert.True(httpScriptInvocationContext.Data.Keys.Contains(item.name));
                Assert.Equal(JsonConvert.SerializeObject(item.val), httpScriptInvocationContext.Data[item.name]);
            }
        }

        private async void ValidateSimpleHttpTriggerInvocationRequest(HttpRequestMessage httpRequestMessage)
        {
            Assert.Contains($"{HttpInvokerConstants.UserAgentHeaderValue}/{ScriptHost.Version}", httpRequestMessage.Headers.UserAgent.ToString());
            Assert.Equal(_testInvocationId.ToString(), httpRequestMessage.Headers.GetValues(HttpInvokerConstants.InvocationIdHeaderName).Single());
            Assert.Equal(ScriptHost.Version, httpRequestMessage.Headers.GetValues(HttpInvokerConstants.HostVersionHeaderName).Single());
            Assert.Equal($"http://localhost:{_defaultPort}/{TestFunctionName}{HttpInvokerTestUtilities.QueryParamString}", httpRequestMessage.RequestUri.AbsoluteUri);
            Assert.Equal(HttpInvokerTestUtilities.AcceptHeaderValue, httpRequestMessage.Headers.GetValues(HeaderNames.Accept).FirstOrDefault());
            Assert.Equal(HttpInvokerTestUtilities.UTF8AcceptCharset, httpRequestMessage.Headers.GetValues(HeaderNames.AcceptCharset).FirstOrDefault());
            var content = await httpRequestMessage.Content.ReadAsStringAsync();
            Assert.Equal($"\"{HttpInvokerTestUtilities.HttpContentStringValue}\"", content);
        }

        private void RequestHandler(HttpRequestMessage httpRequestMessage)
        {
            //used for tests that do not need request validation
        }
    }
}
