// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.Rpc
{
    public class WorkerConfigFactoryTests
    {
        private Mock<IEnvironment> _mockEnvironment = new Mock<IEnvironment>();

        [Fact]
        public void DefaultLanguageWorkersDir()
        {
            var expectedWorkersDir = Path.Combine(Path.GetDirectoryName(new Uri(typeof(WorkerConfigFactory).Assembly.CodeBase).LocalPath), LanguageWorkerConstants.DefaultWorkersDirectoryName);
            var config = new ConfigurationBuilder().Build();
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _mockEnvironment.Object);
            Assert.Equal(expectedWorkersDir, configFactory.WorkersDirPath);
        }

        [Fact]
        public void LanguageWorker_WorkersDir_Set()
        {
            var expectedWorkersDir = @"d:\testWorkersDir";
            var config = new ConfigurationBuilder()
                   .AddInMemoryCollection(new Dictionary<string, string>
                   {
                       [$"{LanguageWorkerConstants.LanguageWorkersSectionName}:{LanguageWorkerConstants.WorkersDirectorySectionName}"] = expectedWorkersDir
                   })
                   .Build();
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _mockEnvironment.Object);
            Assert.Equal(expectedWorkersDir, configFactory.WorkersDirPath);
        }

        [Fact]
        public void LanguageWorker_WorkersDir_NotSet()
        {
            var expectedWorkersDir = Path.Combine(Path.GetDirectoryName(new Uri(typeof(WorkerConfigFactory).Assembly.CodeBase).LocalPath), LanguageWorkerConstants.DefaultWorkersDirectoryName);
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                   .AddInMemoryCollection(new Dictionary<string, string>
                   {
                       ["languageWorker"] = "test"
                   });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _mockEnvironment.Object);
            Assert.Equal(expectedWorkersDir, configFactory.WorkersDirPath);
        }

        [Fact]
        public void JavaPath_AppServiceEnv()
        {
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _mockEnvironment.Object);
            var testEnvVariables = new Dictionary<string, string>
            {
                { EnvironmentSettingNames.AzureWebsiteInstanceId, "123" },
                { "JAVA_HOME", @"D:\Program Files\Java\jdk1.7.0_51" }
            };
            using (var variables = new TestScopedSettings(scriptSettingsManager, testEnvVariables))
            {
                var javaPath = configFactory.GetExecutablePathForJava("../../zulu8.23.0.3-jdk8.0.144-win_x64/bin/java");
                Assert.Equal(@"D:\Program Files\Java\zulu8.23.0.3-jdk8.0.144-win_x64\bin\java", javaPath);
            }
        }

        [Fact]
        public void JavaPath_AppServiceEnv_JavaHomeSet_AppServiceEnvOverrides()
        {
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _mockEnvironment.Object);
            var testEnvVariables = new Dictionary<string, string>
            {
                { EnvironmentSettingNames.AzureWebsiteInstanceId, "123" },
                { "JAVA_HOME", @"D:\Program Files\Java\zulu8.31.0.2-jre8.0.181-win_x64" }
            };
            using (var variables = new TestScopedSettings(scriptSettingsManager, testEnvVariables))
            {
                var javaPath = configFactory.GetExecutablePathForJava("../../zulu8.23.0.3-jdk8.0.144-win_x64/bin/java");
                Assert.Equal(@"D:\Program Files\Java\zulu8.23.0.3-jdk8.0.144-win_x64\bin\java", javaPath);
            }
        }

        [Fact]
        public void JavaPath_JavaHome_Set()
        {
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _mockEnvironment.Object);
            var testEnvVariables = new Dictionary<string, string>
            {
                { "JAVA_HOME", @"D:\Program Files\Java\jdk1.7.0_51" }
            };
            using (var variables = new TestScopedSettings(scriptSettingsManager, testEnvVariables))
            {
                var javaPath = configFactory.GetExecutablePathForJava("java");
                Assert.Equal(@"D:\Program Files\Java\jdk1.7.0_51\bin\java", javaPath);
            }
        }

        [Fact]
        public void JavaPath_JavaHome_Set_DefaultExePathSet()
        {
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _mockEnvironment.Object);
            var testEnvVariables = new Dictionary<string, string>
            {
                { "JAVA_HOME", @"D:\Program Files\Java\jdk1.7.0_51" }
            };
            using (var variables = new TestScopedSettings(scriptSettingsManager, testEnvVariables))
            {
                var javaPath = configFactory.GetExecutablePathForJava(@"D:\MyCustomPath\Java");
                Assert.Equal(@"D:\MyCustomPath\Java", javaPath);
            }
        }

        [Fact]
        public void JavaPath_JavaHome_NotSet()
        {
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _mockEnvironment.Object);
            var testEnvVariables = new Dictionary<string, string>
            {
                { "JAVA_HOME", string.Empty }
            };
            using (var variables = new TestScopedSettings(scriptSettingsManager, testEnvVariables))
            {
                var javaPath = configFactory.GetExecutablePathForJava("java");
                Assert.Equal("java", javaPath);
            }
        }

        [Theory]
        [InlineData("-Djava.net.preferIPv4Stack = true", "-Djava.net.preferIPv4Stack = true")]
        [InlineData("-agentlib:jdwp=transport=dt_socket,server=y,suspend=n,address=5005", "")]
        public void AddArgumentsFromAppSettings_JavaOpts(string expectedArgument, string javaOpts)
        {
            WorkerDescription workerDescription = new WorkerDescription()
            {
                Arguments = new List<string>() { "-jar" },
                DefaultExecutablePath = "java",
                DefaultWorkerPath = "javaworker.jar",
                Extensions = new List<string>() { ".jar" },
                Language = "java"
            };
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test",
                      ["languageWorkers:java:arguments"] = "-agentlib:jdwp=transport=dt_socket,server=y,suspend=n,address=5005"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _mockEnvironment.Object);
            var languageSection = config.GetSection("languageWorkers:java");
            var testEnvVariables = new Dictionary<string, string>
            {
                { "JAVA_OPTS", javaOpts }
            };
            using (var variables = new TestScopedSettings(scriptSettingsManager, testEnvVariables))
            {
                WorkerConfigFactory.AddArgumentsFromAppSettings(workerDescription, languageSection);
                Assert.Equal(2, workerDescription.Arguments.Count);
                Assert.Equal(expectedArgument, workerDescription.Arguments[1]);
            }
        }

        [Fact]
        public void HttpInvokerConfig_Validation_Succeeds()
        {
            Mock<IEnvironment> mockEnvironment = new Mock<IEnvironment>();
            mockEnvironment.Setup(p => p.GetEnvironmentVariable(EnvironmentSettingNames.FunctionsHttpInvoker)).Returns("1");
            string expectedWorkersDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var config = new ConfigurationBuilder()
                   .AddInMemoryCollection(new Dictionary<string, string>
                   {
                       [$"{LanguageWorkerConstants.HttpInvokerSectionName}:{LanguageWorkerConstants.WorkerDirectorySectionName}"] = expectedWorkersDir,
                   })
                   .Build();

            JObject testHttpInvokerConfig = WorkerConfigTestUtilities.GetTestHttpInvokerConfig(null);
            TestLanguageWorkerConfig testLanguageWorkerConfig = new TestLanguageWorkerConfig()
            {
                Json = testHttpInvokerConfig.ToString()
            };
            WorkerConfigTestUtilities.CreateWorkerFolder(expectedWorkersDir, testLanguageWorkerConfig, false);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, mockEnvironment.Object);
            IList<WorkerConfig> workerConfigs = configFactory.GetConfigs();
            Assert.Equal(workerConfigs.Count, 1);
            var expectedDefaultExecutablePath = Path.Combine(expectedWorkersDir, WorkerConfigTestUtilities.HttpInvokerExe);
            Assert.Equal(workerConfigs[0].Description.DefaultExecutablePath, expectedDefaultExecutablePath);
        }

        [Fact]
        public void HttpInvokerConfig_With_Args()
        {
            Mock<IEnvironment> mockEnvironment = new Mock<IEnvironment>();
            mockEnvironment.Setup(p => p.GetEnvironmentVariable(EnvironmentSettingNames.FunctionsHttpInvoker)).Returns("1");
            string expectedWorkersDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var expectedDefaultExecutablePath = Path.Combine(expectedWorkersDir, WorkerConfigTestUtilities.HttpInvokerExe);
            var config = new ConfigurationBuilder()
                   .AddInMemoryCollection(new Dictionary<string, string>
                   {
                       [$"{LanguageWorkerConstants.HttpInvokerSectionName}:{LanguageWorkerConstants.WorkerDirectorySectionName}"] = expectedWorkersDir,
                   })
                   .Build();
            string[] invokerArgs = new string[] { "debugport:10001" };
            JObject testHttpInvokerConfig = WorkerConfigTestUtilities.GetTestHttpInvokerConfig(invokerArgs);
            TestLanguageWorkerConfig testLanguageWorkerConfig = new TestLanguageWorkerConfig()
            {
                Json = testHttpInvokerConfig.ToString()
            };
            WorkerConfigTestUtilities.CreateWorkerFolder(expectedWorkersDir, testLanguageWorkerConfig, false);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, mockEnvironment.Object);
            IList<WorkerConfig> workerConfigs = configFactory.GetConfigs();
            Assert.Equal(workerConfigs.Count, 1);
            Assert.Equal(workerConfigs[0].Description.DefaultExecutablePath, expectedDefaultExecutablePath);
            Assert.Equal(workerConfigs[0].Description.Arguments.Count, 1);
            Assert.Equal(workerConfigs[0].Description.Arguments[0], "debugport:10001");
        }

        [Fact]
        public void HttpInvokerConfig_Validation_Fails()
        {
            Mock<IEnvironment> mockEnvironment = new Mock<IEnvironment>();
            mockEnvironment.Setup(p => p.GetEnvironmentVariable(EnvironmentSettingNames.FunctionsHttpInvoker)).Returns("1");
            string expectedWorkersDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var config = new ConfigurationBuilder()
                   .AddInMemoryCollection(new Dictionary<string, string>
                   {
                       [$"{LanguageWorkerConstants.HttpInvokerSectionName}:{LanguageWorkerConstants.WorkerDirectorySectionName}"] = expectedWorkersDir,
                   })
                   .Build();

            JObject testHttpInvokerConfig = WorkerConfigTestUtilities.GetTestHttpInvokerConfig(null, true);
            TestLanguageWorkerConfig testLanguageWorkerConfig = new TestLanguageWorkerConfig()
            {
                Json = testHttpInvokerConfig.ToString()
            };
            WorkerConfigTestUtilities.CreateWorkerFolder(expectedWorkersDir, testLanguageWorkerConfig, false);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, mockEnvironment.Object);
            IList<WorkerConfig> workerConfigs = configFactory.GetConfigs();
            Assert.Equal(workerConfigs.Count, 0);
        }
    }
}