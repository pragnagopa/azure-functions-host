// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.WebJobs.Script.Tests;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.EndToEnd
{
    [Trait(TestTraits.Category, TestTraits.EndToEnd)]
    [Trait(TestTraits.Group, TestTraits.SamplesEndToEnd)]
    public class SamplesEndToEndTests_Java : IClassFixture<SamplesEndToEndTests_Java.TestFixture>
    {
        private readonly ScriptSettingsManager _settingsManager;
        private TestFixture _fixture;

        public SamplesEndToEndTests_Java(TestFixture fixture)
        {
            _fixture = fixture;
            _settingsManager = ScriptSettingsManager.Instance;
        }

        [Fact]
        public async Task HttpTrigger_Java_Get_Succeeds()
        {
            await InvokeHttpTrigger("HttpTrigger");
        }

        [Fact]
        public async Task JavaProcess_Same_AfterHostRestart()
        {
            string runtime = "java";
            IEnumerable<int> javaProcessesBefore = Process.GetProcessesByName(runtime).Select(p => p.Id);
            ILanguageWorkerChannelManager languageWorkerChannelManager = (ILanguageWorkerChannelManager)_fixture.GetHostServices().GetService(typeof(ILanguageWorkerChannelManager));
            IFunctionDispatcher functionDispatcher = (IFunctionDispatcher)_fixture.GetHostServices().GetService(typeof(IFunctionDispatcher));
            var languageWorkerState = GetWorkerState(functionDispatcher, runtime);

            var workerIdBeforeRestart = languageWorkerState.Channel.WorkerId;

            // Assert.Null(languageWorkerChannelManager.GetChannel(runtime)?.WorkerId);
            Assert.Null(languageWorkerState.WorkerProcess);

            await HttpTrigger_Java_Get_Succeeds();

            // Trigger a restart
            await _fixture.Host.RestartAsync(CancellationToken.None);
            
            // Verify function invocation succeeds
            await HttpTrigger_Java_Get_Succeeds();

            // Verify LanguageWorkerChannel is reinitialized
            languageWorkerState = GetWorkerState(functionDispatcher, runtime);
            // Assert.NotEqual(workerIdBeforeRestart, languageWorkerState.Channel.WorkerId);
            Assert.Null(languageWorkerState.WorkerProcess);

            IEnumerable<int> javaProcessesAfter = Process.GetProcessesByName("java").Select(p => p.Id);
            // Verify number of java processes before and after restart are the same.
            Assert.Equal(javaProcessesBefore.Count(), javaProcessesAfter.Count());
            // Verify Java same java process is used after host restart
            var result = javaProcessesBefore.Where(pId1 => !javaProcessesAfter.Any(pId2 => pId2 == pId1));
            Assert.Equal(0, result.Count());
        }

        private async Task InvokeHttpTrigger(string functionName)
        {
            string functionKey = await _fixture.Host.GetFunctionSecretAsync($"{functionName}");
            string uri = $"api/{functionName}?code={functionKey}&name=Mathew";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

            var response = await _fixture.Host.HttpClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private LanguageWorkerState GetWorkerState(IFunctionDispatcher functionDispatcher, string runtime)
        {
            return functionDispatcher.LanguageWorkerChannelStates.Where(lw => lw.Key == runtime).FirstOrDefault().Value;
        }

        public class TestFixture : EndToEndTestFixture
        {
            static TestFixture()
            {
            }

            public TestFixture()
                : base(Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\..\sample\java"), "samples", LanguageWorkerConstants.JavaLanguageWorkerName)
            {
            }

            public override void ConfigureJobHost(IWebJobsBuilder webJobsBuilder)
            {
                base.ConfigureJobHost(webJobsBuilder);
            }
        }
    }
}