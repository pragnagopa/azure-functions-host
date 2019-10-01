//// Copyright (c) .NET Foundation. All rights reserved.
//// Licensed under the MIT License. See License.txt in the project root for license information.

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.Azure.WebJobs.Script.Description;
//using Microsoft.Azure.WebJobs.Script.OutOfProc;
//using Microsoft.Azure.WebJobs.Script.OutOfProc.Http;
//using Moq;
//using Moq.Protected;
//using Newtonsoft.Json.Linq;
//using Xunit;

//namespace Microsoft.Azure.WebJobs.Script.Tests.HttpInvoker
//{
//    public class DefaultHttpInvokerServiceTests
//    {
//        private HttpClient _httpClient;
//        private DefaultHttpInvokerService _defaultHttpInvokerService;
//        private Guid _testInvocationId;

//        public DefaultHttpInvokerServiceTests()
//        {
//            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

//            handlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
//                ItExpr.IsAny<HttpRequestMessage>(),
//                ItExpr.IsAny<CancellationToken>())
//                .Callback<HttpRequestMessage, CancellationToken>((request, token) => ValidateRequest(request))
//                .ReturnsAsync(new HttpResponseMessage
//                {
//                    StatusCode = HttpStatusCode.OK
//                });

//            _httpClient = new HttpClient(handlerMock.Object);
//            _defaultHttpInvokerService = new DefaultHttpInvokerService(_httpClient);
//            _testInvocationId = Guid.NewGuid();
//        }

//        [Fact]
//        public async Task DefaultHttpTriggerRequest_Expected()
//        {
//            string requestUri = "http://localhost:8090/";
//            await _defaultHttpInvokerService.ProcessDefaultInvocationRequest(GetScriptInvocationContext("testFunction", _testInvocationId), requestUri);
//        }

//        public static ScriptInvocationContext GetScriptInvocationContext(string functionName, Guid invocationId)
//        {
//            ScriptInvocationContext scriptInvocationContext = new ScriptInvocationContext()
//            {
//                ExecutionContext = new ExecutionContext()
//                {
//                    InvocationId = invocationId,
//                    FunctionName = functionName,
//                },
//                BindingData = new Dictionary<string, object>(),
//                Inputs = new List<(string name, DataType type, object val)>(),
//                ResultSource = new TaskCompletionSource<ScriptInvocationResult>()
//            };
//            var functionMetadata = new FunctionMetadata
//            {
//                Name = functionName
//            };
//            var httpTriggerBinding = new BindingMetadata
//            {
//                Name = "req",
//                Type = "httpTrigger",
//                Direction = BindingDirection.In,
//                Raw = new JObject()
//            };
//            var queueInputBinding = new BindingMetadata
//            {
//                Name = "queueInput",
//                Type = "queue",
//                Direction = BindingDirection.In
//            };
//            var httpOutputBinding = new BindingMetadata
//            {
//                Name = "res",
//                Type = "http",
//                Direction = BindingDirection.Out,
//                Raw = new JObject()
//            };
//            scriptInvocationContext.FunctionMetadata = functionMetadata;
//            scriptInvocationContext.Inputs = new List<(string name, DataType type, object val)>();
//            return scriptInvocationContext;
//        }

//        private void ValidateRequest(HttpRequestMessage httpRequestMessage)
//        {
//            Assert.Equal(HttpInvokerConstants.UserAgentHeaderValue, httpRequestMessage.Headers.GetValues("User-Agent").Single());
//            Assert.Equal(_testInvocationId.ToString(), httpRequestMessage.Content.Headers.GetValues(HttpInvokerConstants.InvocationIdHeaderName).Single());
//            Assert.StartsWith("2.0", httpRequestMessage.Content.Headers.GetValues(HttpInvokerConstants.HostVersionHeaderName).Single());

//            Assert.Equal(httpRequestMessage.RequestUri.ToString(), "http://localhost:8090/testFunction");

//            //if (requestPath.Contains(LinuxContainerMetricsPublisher.PublishFunctionActivityPath))
//            //{
//            //    ObjectContent requestContent = (ObjectContent)request.Content;
//            //    Assert.Equal(requestContent.Value.GetType(), typeof(FunctionActivity[]));

//            //    IEnumerable<FunctionActivity> activitesPayload = (FunctionActivity[])requestContent.Value;
//            //    Assert.Equal(activitesPayload.Single().FunctionName, _testFunctionActivity.FunctionName);
//            //    Assert.Equal(activitesPayload.Single().InvocationId, _testFunctionActivity.InvocationId);
//            //    Assert.Equal(activitesPayload.Single().Concurrency, _testFunctionActivity.Concurrency);
//            //    Assert.Equal(activitesPayload.Single().ExecutionStage, _testFunctionActivity.ExecutionStage);
//            //    Assert.Equal(activitesPayload.Single().IsSucceeded, _testFunctionActivity.IsSucceeded);
//            //    Assert.Equal(activitesPayload.Single().ExecutionTimeSpanInMs, _testFunctionActivity.ExecutionTimeSpanInMs);
//            //    Assert.Equal(activitesPayload.Single().Tenant, _testTenant);
//            //    Assert.Equal(activitesPayload.Single().EventTimeStamp, _testFunctionActivity.EventTimeStamp);
//            //}
//            //else if (requestPath.Contains(LinuxContainerMetricsPublisher.PublishMemoryActivityPath))
//            //{
//            //    ObjectContent requestContent = (ObjectContent)request.Content;
//            //    Assert.Equal(requestContent.Value.GetType(), typeof(MemoryActivity[]));

//            //    IEnumerable<MemoryActivity> activitesPayload = (MemoryActivity[])requestContent.Value;
//            //    Assert.Equal(activitesPayload.Single().CommitSizeInBytes, _testMemoryActivity.CommitSizeInBytes);
//            //    Assert.Equal(activitesPayload.Single().EventTimeStamp, _testMemoryActivity.EventTimeStamp);
//            //    Assert.Equal(activitesPayload.Single().Tenant, _testTenant);
//            //}
//        }
//    }
//}
