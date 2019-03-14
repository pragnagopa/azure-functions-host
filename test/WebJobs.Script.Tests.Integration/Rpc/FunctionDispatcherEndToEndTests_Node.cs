// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests
{
    public class FunctionDispatcherEndToEndTests_Node : IClassFixture<FunctionDispatcherEndToEndTests_Node.TestFixture>
    {
        private IDictionary<string, LanguageWorkerState> _channelStates;
        private LanguageWorkerChannel _nodeWorkerChannel;
        private ILanguageWorkerProcess _nodeWorkerProcess;
        private string _functionName = "HttpTrigger";
        private string _rutnime = LanguageWorkerConstants.NodeLanguageWorkerName;

        public FunctionDispatcherEndToEndTests_Node(TestFixture fixture)
        {
            Fixture = fixture;
            _channelStates = Fixture.JobHost.FunctionDispatcher.LanguageWorkerChannelStates;
            _nodeWorkerChannel = RpcTestHelpers.GetCurrentWorkerChannel(_channelStates, _rutnime);
            _nodeWorkerProcess = RpcTestHelpers.GetCurrentWorkerProcess(_channelStates, _rutnime);
        }

        public TestFixture Fixture { get; set; }

        [Fact]
        public async Task InitializeWorkers_Fails_AddsFunctionErrors_Node()
        {
            for (int i = 0; i < 3; i++)
            {
                RpcTestHelpers.KillProcess(_nodeWorkerProcess.WorkerProcess.Id);
                await RpcTestHelpers.WaitForWorkerProcessRestart(_channelStates, _rutnime, _nodeWorkerChannel.WorkerId, Fixture, _functionName);
                _nodeWorkerChannel = RpcTestHelpers.GetCurrentWorkerChannel(_channelStates, _rutnime);
                _nodeWorkerProcess = RpcTestHelpers.GetCurrentWorkerProcess(_channelStates, _rutnime);
            }

            ICollection<string> actualFunctionErrors = Fixture.JobHost.FunctionErrors[_functionName];
            Assert.NotNull(actualFunctionErrors);
            Assert.Contains("Failed to start language worker process for: node", actualFunctionErrors.First());
        }

        public class TestFixture : ScriptHostEndToEndTestFixture
        {
            public TestFixture() : base(@"TestScripts\Node", "node", LanguageWorkerConstants.NodeLanguageWorkerName,
                startHost: true, functions: new[] { "HttpTrigger" })
            {
            }
        }
    }
}