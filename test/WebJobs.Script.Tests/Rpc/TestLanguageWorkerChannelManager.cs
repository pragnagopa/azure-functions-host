// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.ManagedDependencies;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.Tests.Rpc
{
    public class TestLanguageWorkerChannelManager : ILanguageWorkerChannelManager
    {
        private IScriptEventManager _eventManager;
        private ILogger _testLogger;
        private IDictionary<string, LanguageWorkerState> _workerStates = new Dictionary<string, LanguageWorkerState>();
        private string _scriptRootPath;

        public TestLanguageWorkerChannelManager(IScriptEventManager eventManager, ILogger testLogger, string scriptRootPath)
        {
            _eventManager = eventManager;
            _testLogger = testLogger;
            _scriptRootPath = scriptRootPath;
        }

        public ILanguageWorkerChannel CreateLanguageWorkerChannel(string workerId, string scriptRootPath, string language, IMetricsLogger metricsLogger, int attemptCount, bool isWebhostChannel = false, IOptions<ManagedDependencyOptions> managedDependencyOptions = null)
        {
            var languageWorkerChannel = new TestLanguageWorkerChannel(workerId, language, _eventManager, _testLogger, isWebhostChannel);
            if (isWebhostChannel)
            {
                if (_workerStates.TryGetValue(language, out LanguageWorkerState workerState))
                {
                    workerState.AddChannel(languageWorkerChannel);
                }
                else
                {
                    _workerStates.Add(language, new LanguageWorkerState());
                    _workerStates[language].AddChannel(languageWorkerChannel);
                }
            }
            return languageWorkerChannel;
        }

        public ILanguageWorkerChannel GetChannel(string language)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<ILanguageWorkerChannel> GetChannels(string language)
        {
            if (_workerStates.TryGetValue(language, out LanguageWorkerState workerState))
            {
                return workerState.GetChannels();
            }
            return null;
        }

        public Task<ILanguageWorkerChannel> InitializeChannelAsync(string language)
        {
            ILanguageWorkerChannel workerChannel = CreateLanguageWorkerChannel(Guid.NewGuid().ToString(), _scriptRootPath, language, null, 0, true);
            workerChannel.StartWorkerProcess();
            return Task.FromResult(workerChannel);
        }

        public void ShutdownChannels()
        {
        }

        public bool ShutdownChannelsIfExist(string language)
        {
            return true;
        }

        public void ShutdownStandbyChannels(IEnumerable<FunctionMetadata> functions)
        {
        }

        public Task SpecializeAsync()
        {
            throw new System.NotImplementedException();
        }
    }
}
