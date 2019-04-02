// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FunctionMetadata = Microsoft.Azure.WebJobs.Script.Description.FunctionMetadata;

namespace Microsoft.Azure.WebJobs.Script.Tests.Rpc
{
    public class FunctionDispatcherTests
    {
        [Theory]
        [InlineData("node", "node")]
        [InlineData("java", "java")]
        [InlineData("", "node")]
        [InlineData(null, "java")]
        public void IsSupported_Returns_True(string language, string funcMetadataLanguage)
        {
            IFunctionDispatcher functionDispatcher = GetTestFunctionDispatcher();
            FunctionMetadata func1 = new FunctionMetadata()
            {
                Name = "func1",
                Language = funcMetadataLanguage
            };
            Assert.True(functionDispatcher.IsSupported(func1, language));
        }

        [Theory]
        [InlineData("node", "java")]
        [InlineData("java", "node")]
        [InlineData("python", "")]
        public void IsSupported_Returns_False(string language, string funcMetadataLanguage)
        {
            IFunctionDispatcher functionDispatcher = GetTestFunctionDispatcher();
            FunctionMetadata func1 = new FunctionMetadata()
            {
                Name = "func1",
                Language = funcMetadataLanguage
            };
            Assert.False(functionDispatcher.IsSupported(func1, language));
        }

        [Fact]
        public async void Starting_MultipleJobhostChannels_Succeeds()
        {
            int expectedProcessCount = 5;
            IFunctionDispatcher functionDispatcher = GetTestFunctionDispatcher(expectedProcessCount.ToString());
            functionDispatcher.Initialize("node", new List<FunctionMetadata>());
            var finalChannelCount = await WaitForJobhostWorkerChannelsToStartup(functionDispatcher, expectedProcessCount);
            Assert.Equal(finalChannelCount, expectedProcessCount);
        }

        [Fact]
        public async void Starting_MultipleWebhostChannels_Succeeds()
        {
            int expectedProcessCount = 5;
            FunctionDispatcher functionDispatcher = (FunctionDispatcher)GetTestFunctionDispatcher(expectedProcessCount.ToString(), true);
            functionDispatcher.Initialize("java", new List<FunctionMetadata>());

            var finalWebhostChannelCount = await WaitForWebhostWorkerChannelsToStartup(functionDispatcher.ChannelManager, expectedProcessCount, "java");
            Assert.Equal(finalWebhostChannelCount, expectedProcessCount);

            var finalJobhostChannelCount = functionDispatcher.LanguageWorkerChannelState.GetChannels().Count();
            Assert.Equal(finalJobhostChannelCount, 0);
        }

        [Fact]
        public void MaxProcessCount_Returns_Default()
        {
            FunctionDispatcher functionDispatcher = (FunctionDispatcher)GetTestFunctionDispatcher();
            Assert.Equal(functionDispatcher.MaxProcessCount, 1);

            functionDispatcher = (FunctionDispatcher)GetTestFunctionDispatcher("0");
            Assert.Equal(functionDispatcher.MaxProcessCount, 1);

            functionDispatcher = (FunctionDispatcher)GetTestFunctionDispatcher("-1");
            Assert.Equal(functionDispatcher.MaxProcessCount, 1);
        }

        [Fact]
        public void MaxProcessCount_ProcessCount_Set_Returns_ExpectedCount()
        {
            int expectedProcessCount = 3;
            FunctionDispatcher functionDispatcher = (FunctionDispatcher)GetTestFunctionDispatcher(expectedProcessCount.ToString());
            Assert.Equal(functionDispatcher.MaxProcessCount, expectedProcessCount);
        }

        private static IFunctionDispatcher GetTestFunctionDispatcher(string maxProcessCountValue = null, bool addWebhostChannel = false)
        {
            var eventManager = new ScriptEventManager();
            var metricsLogger = new Mock<IMetricsLogger>();
            var testEnv = new TestEnvironment();

            if (!string.IsNullOrEmpty(maxProcessCountValue))
            {
                testEnv.SetEnvironmentVariable(LanguageWorkerConstants.FunctionsWorkerProcessCountSettingName, maxProcessCountValue);
            }

            var loggerFactory = MockNullLoggerFactory.CreateLoggerFactory();
            var testLogger = new TestLogger("FunctionDispatcherTests");

            var options = new ScriptJobHostOptions
            {
                RootLogPath = Path.GetTempPath()
            };

            IOptions<ScriptJobHostOptions> scriptOptions = new OptionsManager<ScriptJobHostOptions>(new TestOptionsFactory<ScriptJobHostOptions>(options));

            var workerConfigOptions = new LanguageWorkerOptions
            {
                WorkerConfigs = TestHelpers.GetTestWorkerConfigs()
            };

            var languageWorkerChannelManager = new TestLanguageWorkerChannelManager(eventManager, testLogger, scriptOptions.Value.RootScriptPath);
            if (addWebhostChannel)
            {
                languageWorkerChannelManager.InitializeChannelAsync("java");
            }
            return new FunctionDispatcher(scriptOptions, metricsLogger.Object, testEnv, eventManager, loggerFactory, new OptionsWrapper<LanguageWorkerOptions>(workerConfigOptions), languageWorkerChannelManager, null);
        }

        private async Task<int> WaitForJobhostWorkerChannelsToStartup(IFunctionDispatcher functionDispatcher, int expectedCount)
        {
            int currentChannelCount = 0;
            await TestHelpers.Await(() =>
            {
                currentChannelCount = functionDispatcher.LanguageWorkerChannelState.GetChannels().Count();
                return currentChannelCount == expectedCount;
            }, pollingInterval: 4 * 1000, timeout: 60 * 1000);
            return currentChannelCount;
        }

        private async Task<int> WaitForWebhostWorkerChannelsToStartup(ILanguageWorkerChannelManager channelManager, int expectedCount, string language)
        {
            int currentChannelCount = 0;
            await TestHelpers.Await(() =>
            {
                currentChannelCount = channelManager.GetChannels(language).Count();
                return currentChannelCount == expectedCount;
            }, pollingInterval: 4 * 1000, timeout: 60 * 1000);
            return currentChannelCount;
        }
    }
}
