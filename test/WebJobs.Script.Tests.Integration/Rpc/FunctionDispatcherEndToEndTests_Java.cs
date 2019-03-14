// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests
{
    public class FunctionDispatcherEndToEndTests_Java : IClassFixture<FunctionDispatcherEndToEndTests_Java.TestFixture>
    {
        private IDictionary<string, LanguageWorkerState> _channelStates;
        private LanguageWorkerChannel _javaWorkerChannel;
        private ILanguageWorkerProcess _javaWorkerProcess;
        private string _functionName = "HttpTrigger";
        private string _rutnime = LanguageWorkerConstants.JavaLanguageWorkerName;

        public FunctionDispatcherEndToEndTests_Java(TestFixture fixture)
        {
            Fixture = fixture;
            _channelStates = Fixture.JobHost.FunctionDispatcher.LanguageWorkerChannelStates;
            _javaWorkerChannel = RpcTestHelpers.GetCurrentWorkerChannel(_channelStates, _rutnime);
            _javaWorkerProcess = RpcTestHelpers.GetCurrentWorkerProcess(_channelStates, _rutnime);
        }

        public TestFixture Fixture { get; set; }

        [Fact]
        public async Task InitializeWorkers_Fails_AddsFunctionErrors_Java()
        {
            for (int i = 0; i < 3; i++)
            {
                RpcTestHelpers.KillProcess(_javaWorkerProcess.WorkerProcess.Id);
                await RpcTestHelpers.WaitForWorkerProcessRestart(_channelStates, _rutnime, _javaWorkerChannel.WorkerId, Fixture, _functionName);
                _javaWorkerChannel = RpcTestHelpers.GetCurrentWorkerChannel(_channelStates, _rutnime);
                _javaWorkerProcess = RpcTestHelpers.GetCurrentWorkerProcess(_channelStates, _rutnime);
            }

            ICollection<string> actualFunctionErrors = Fixture.JobHost.FunctionErrors[_functionName];
            Assert.NotNull(actualFunctionErrors);
            Assert.Contains("Failed to start language worker process for: java", actualFunctionErrors.First());
        }

        //[Fact]
        public async Task JobHostRestarts_LanguageWorkerChannel_ReInitialized_WorkerProcess_DoesnotRetart_Java()
        {
            int javaProcessIdBeforeJobHostRestart = _javaWorkerProcess.WorkerProcess.Id;
            string javaChannelIdBeforeJobHostRestart = _javaWorkerChannel.WorkerId;
            // now "touch" a file in the shared directory to trigger a restart
            string sharedModulePath = Path.Combine(Fixture.JobHost.ScriptOptions.RootScriptPath, $"{_functionName}\\function.json");
            File.AppendAllText(sharedModulePath, Environment.NewLine);
            await RpcTestHelpers.WaitForJobHostRestart(_channelStates, _rutnime, _javaWorkerChannel.WorkerId);
            _javaWorkerChannel = RpcTestHelpers.GetCurrentWorkerChannel(_channelStates, _rutnime);
            _javaWorkerProcess = RpcTestHelpers.GetCurrentWorkerProcess(_channelStates, _rutnime);
            Assert.Equal(javaProcessIdBeforeJobHostRestart, _javaWorkerProcess.WorkerProcess.Id);
            Assert.NotEqual(javaChannelIdBeforeJobHostRestart, _javaWorkerChannel.WorkerId);
        }

        public class TestFixture : ScriptHostEndToEndTestFixture
        {
            public TestFixture() : base(@"TestScripts\java", "java", LanguageWorkerConstants.JavaLanguageWorkerName,
                startHost: true, functions: new[] { "HttpTrigger" })
            {
            }
        }
    }
}