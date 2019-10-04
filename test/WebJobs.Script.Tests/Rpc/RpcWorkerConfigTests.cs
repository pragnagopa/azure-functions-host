// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Azure.WebJobs.Script.OutOfProc;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.Rpc
{
    public class RpcWorkerConfigTests : IDisposable
    {
        private static string rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static string customRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static string testLanguagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static string testLanguage = "testLanguage";

        private TestSystemRuntimeInformation _testSysRuntimeInfo = new TestSystemRuntimeInformation();
        private TestEnvironment _testEnvironment;

        public RpcWorkerConfigTests()
        {
            _testEnvironment = new TestEnvironment();
        }

        public static IEnumerable<object[]> InvalidWorkerDescriptions
        {
            get
            {
                yield return new object[] { new RpcWorkerDescription() { Extensions = new List<string>() } };
                yield return new object[] { new RpcWorkerDescription() { Language = testLanguage } };
                yield return new object[] { new RpcWorkerDescription() { Language = testLanguage, Extensions = new List<string>() } };
            }
        }

        public static IEnumerable<object[]> ValidWorkerDescriptions
        {
            get
            {
                yield return new object[] { new RpcWorkerDescription() { Language = testLanguage, Extensions = new List<string>(), DefaultExecutablePath = "test.exe" } };
                yield return new object[] { new RpcWorkerDescription() { Language = testLanguage, Extensions = new List<string>(), DefaultExecutablePath = "test.exe", DefaultWorkerPath = WorkerConfigTestUtilities.TestDefaultWorkerFile } };
                yield return new object[] { new RpcWorkerDescription() { Language = testLanguage, Extensions = new List<string>(), DefaultExecutablePath = "test.exe", DefaultWorkerPath = WorkerConfigTestUtilities.TestDefaultWorkerFile, Arguments = new List<string>() } };
            }
        }

        public void Dispose()
        {
            _testEnvironment.Clear();
        }

        [Fact]
        public void ReadWorkerProviderFromConfig_ReturnsProviderWithArguments()
        {
            var expectedArguments = new string[] { "-v", "verbose" };
            var configs = new List<TestLanguageWorkerConfig>() { MakeTestConfig(testLanguage, expectedArguments) };
            var testLogger = new TestLogger(testLanguage);

            // Creates temp directory w/ worker.config.json and runs ReadWorkerProviderFromConfig
            IEnumerable<WorkerConfig> workerConfigs = TestReadWorkerProviderFromConfig(configs, testLogger);
            Assert.Single(workerConfigs);

            WorkerConfig worker = workerConfigs.FirstOrDefault();
            Assert.True(expectedArguments.SequenceEqual(worker.Description.Arguments.ToArray()));
        }

        [Fact]
        public void ReadWorkerProviderFromConfig_ReturnsProviderNoArguments()
        {
            var configs = new List<TestLanguageWorkerConfig>() { MakeTestConfig(testLanguage, new string[0]) };
            // Creates temp directory w/ worker.config.json and runs ReadWorkerProviderFromConfig
            var workerConfigs = TestReadWorkerProviderFromConfig(configs, new TestLogger(testLanguage));
            Assert.Single(workerConfigs);
            Assert.Equal(Path.Combine(rootPath, testLanguage, $"{WorkerConfigTestUtilities.TestWorkerPathInWorkerConfig}.{testLanguage}"), workerConfigs.Single().Description.DefaultWorkerPath);
            WorkerConfig worker = workerConfigs.FirstOrDefault();
            Assert.True(worker.Description.Arguments.Count == 0);
        }

        [Fact]
        public void ReadWorkerProviderFromConfig_ArgumentsFromSettings()
        {
            var configs = new List<TestLanguageWorkerConfig>() { MakeTestConfig(testLanguage, new string[0]) };
            // Creates temp directory w/ worker.config.json and runs ReadWorkerProviderFromConfig
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>
            {
                [$"{LanguageWorkerConstants.LanguageWorkersSectionName}:{testLanguage}:{OutOfProcConstants.WorkerDescriptionArguments}"] = "--inspect=5689  --no-deprecation"
            };
            var workerConfigs = TestReadWorkerProviderFromConfig(configs, new TestLogger(testLanguage), null, keyValuePairs);
            Assert.Single(workerConfigs);
            Assert.Equal(Path.Combine(rootPath, testLanguage, $"{WorkerConfigTestUtilities.TestWorkerPathInWorkerConfig}.{testLanguage}"), workerConfigs.Single().Description.DefaultWorkerPath);
            WorkerConfig worker = workerConfigs.FirstOrDefault();
            Assert.True(worker.Description.Arguments.Count == 2);
            Assert.True(worker.Description.Arguments.Contains("--inspect=5689"));
            Assert.True(worker.Description.Arguments.Contains("--no-deprecation"));
        }

        [Fact]
        public void ReadWorkerProviderFromConfig_EmptyWorkerPath()
        {
            var configs = new List<TestLanguageWorkerConfig>() { MakeTestConfig(testLanguage, new string[0], false, string.Empty, true) };
            // Creates temp directory w/ worker.config.json and runs ReadWorkerProviderFromConfig
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>
            {
                [$"{LanguageWorkerConstants.LanguageWorkersSectionName}:{testLanguage}:{OutOfProcConstants.WorkerDescriptionArguments}"] = "--inspect=5689  --no-deprecation"
            };
            var workerConfigs = TestReadWorkerProviderFromConfig(configs, new TestLogger(testLanguage));
            Assert.Single(workerConfigs);
            Assert.Null(workerConfigs.Single().Description.DefaultWorkerPath);
        }

        [Fact]
        public void ReadWorkerProviderFromConfig_Concatenate_ArgsFromSettings_ArgsFromWorkerConfig()
        {
            string[] argsFromConfig = new string[] { "--expose-http2", "--no-deprecation" };
            var configs = new List<TestLanguageWorkerConfig>() { MakeTestConfig(testLanguage, argsFromConfig) };
            // Creates temp directory w/ worker.config.json and runs ReadWorkerProviderFromConfig
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>
            {
                [$"{LanguageWorkerConstants.LanguageWorkersSectionName}:{testLanguage}:{OutOfProcConstants.WorkerDescriptionArguments}"] = "--inspect=5689"
            };
            var workerConfigs = TestReadWorkerProviderFromConfig(configs, new TestLogger(testLanguage), null, keyValuePairs);
            Assert.Single(workerConfigs);
            WorkerConfig workerConfig = workerConfigs.Single();
            Assert.Equal(Path.Combine(rootPath, testLanguage, $"{WorkerConfigTestUtilities.TestWorkerPathInWorkerConfig}.{testLanguage}"), workerConfig.Description.DefaultWorkerPath);
            Assert.True(workerConfig.Description.Arguments.Count == 3);
            Assert.True(workerConfig.Description.Arguments.Contains("--inspect=5689"));
            Assert.True(workerConfig.Description.Arguments.Contains("--no-deprecation"));
            Assert.True(workerConfig.Description.Arguments.Contains("--expose-http2"));
        }

        [Fact]
        public void ReadWorkerProviderFromConfig_InvalidConfigFile()
        {
            var configs = new List<TestLanguageWorkerConfig>() { MakeTestConfig(testLanguage, new string[0], true) };
            var testLogger = new TestLogger(testLanguage);
            // Creates temp directory w/ worker.config.json and runs ReadWorkerProviderFromConfig
            var workerConfigs = TestReadWorkerProviderFromConfig(configs, testLogger);
            var logs = testLogger.GetLogMessages();
            var errorLog = logs.Where(log => log.Level == LogLevel.Error).FirstOrDefault();
            Assert.NotNull(errorLog);
            Assert.NotNull(errorLog.Exception);
            Assert.True(errorLog.FormattedMessage.Contains("Failed to initialize"));
            Assert.Empty(workerConfigs);
        }

        [Fact]
        public void ReadWorkerProviderFromAppSetting()
        {
            var testConfig = MakeTestConfig(testLanguage, new string[0]);
            var configs = new List<TestLanguageWorkerConfig>() { testConfig };
            WorkerConfigTestUtilities.CreateWorkerFolder(customRootPath, testConfig);
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>
            {
                [$"{LanguageWorkerConstants.LanguageWorkersSectionName}:{testLanguage}:{OutOfProcConstants.WorkerDirectorySectionName}"] = Path.Combine(customRootPath, testLanguage)
            };

            var workerConfigs = TestReadWorkerProviderFromConfig(configs, new TestLogger(testLanguage), null, keyValuePairs);
            Assert.Single(workerConfigs);
            WorkerConfig workerConfig = workerConfigs.Single();
            Assert.Equal(Path.Combine(customRootPath, testLanguage, $"{WorkerConfigTestUtilities.TestWorkerPathInWorkerConfig}.{testLanguage}"), workerConfig.Description.DefaultWorkerPath);
        }

        [Fact]
        public void ReadWorkerProviderFromConfig_InvalidWorker()
        {
            var testConfig = MakeTestConfig(testLanguage, new string[0]);
            var configs = new List<TestLanguageWorkerConfig>() { testConfig };
            WorkerConfigTestUtilities.CreateWorkerFolder(customRootPath, testConfig, false);
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>
            {
                [$"{LanguageWorkerConstants.LanguageWorkersSectionName}:{testLanguage}:{OutOfProcConstants.WorkerDirectorySectionName}"] = customRootPath
            };

            var workerConfigs = TestReadWorkerProviderFromConfig(configs, new TestLogger(testLanguage), null, keyValuePairs);
            Assert.Empty(workerConfigs);
        }

        [Fact]
        public void ReadWorkerProviderFromConfig_AddProfile_ReturnsDefaultDescription()
        {
            var expectedArguments = new string[] { "-v", "verbose" };
            var configs = new List<TestLanguageWorkerConfig>() { MakeTestConfig(testLanguage, expectedArguments, false, "TestProfile") };
            var testLogger = new TestLogger(testLanguage);

            // Creates temp directory w/ worker.config.json and runs ReadWorkerProviderFromConfig
            IEnumerable<WorkerConfig> workerConfigs = TestReadWorkerProviderFromConfig(configs, testLogger);
            Assert.Single(workerConfigs);

            WorkerConfig worker = workerConfigs.FirstOrDefault();
            Assert.Equal(WorkerConfigTestUtilities.TestDefaultExecutablePath, worker.Description.DefaultExecutablePath);
        }

        [Fact]
        public void ReadWorkerProviderFromConfig_OverrideDefaultExePath()
        {
            var configs = new List<TestLanguageWorkerConfig>() { MakeTestConfig(testLanguage, new string[0], false, OutOfProcConstants.WorkerDescriptionAppServiceEnvProfileName) };
            var testLogger = new TestLogger(testLanguage);
            var testExePath = "./mySrc/myIndex";
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>
            {
                [$"{LanguageWorkerConstants.LanguageWorkersSectionName}:{testLanguage}:{OutOfProcConstants.WorkerDescriptionDefaultExecutablePath}"] = testExePath
            };
            var workerConfigs = TestReadWorkerProviderFromConfig(configs, new TestLogger(testLanguage), null, keyValuePairs, true);
            Assert.Single(workerConfigs);

            WorkerConfig worker = workerConfigs.FirstOrDefault();
            Assert.Equal(testExePath, worker.Description.DefaultExecutablePath);
        }

        [Theory]
        [MemberData(nameof(InvalidWorkerDescriptions))]
        public void InvalidWorkerDescription_Throws(WorkerDescription workerDescription)
        {
            Assert.Throws<ValidationException>(() => workerDescription.ApplyDefaultsAndValidate());
        }

        [Theory]
        [MemberData(nameof(ValidWorkerDescriptions))]
        public void ValidateWorkerDescription_Succeeds(WorkerDescription workerDescription)
        {
            try
            {
                WorkerConfigTestUtilities.CreateTestWorkerFileInCurrentDir();
                workerDescription.ApplyDefaultsAndValidate();
            }
            finally
            {
                WorkerConfigTestUtilities.DeleteTestWorkerFileInCurrentDir();
            }
        }

        [Theory]
        [InlineData("%FUNCTIONS_WORKER_RUNTIME_VERSION%/{os}/{architecture}", "3.7/LINUX/X64")]
        [InlineData("%FUNCTIONS_WORKER_RUNTIME_VERSION%/{architecture}", "3.7/X64")]
        [InlineData("%FUNCTIONS_WORKER_RUNTIME_VERSION%/{os}", "3.7/LINUX")]
        public void LanguageWorker_HydratedWorkerPath_EnvironmentVersionSet(string defaultWorkerPath, string expectedPath)
        {
            _testEnvironment.SetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeVersionSettingName, "3.7");

            RpcWorkerDescription workerDescription = new RpcWorkerDescription()
            {
                Arguments = new List<string>(),
                DefaultExecutablePath = "python",
                DefaultWorkerPath = defaultWorkerPath,
                DefaultRuntimeVersion = "3.6",
                SupportedArchitectures = new List<string>() { Architecture.X64.ToString(), Architecture.X86.ToString() },
                SupportedRuntimeVersions = new List<string>() { "3.6", "3.7" },
                SupportedOperatingSystems = new List<string>()
                    {
                        OSPlatform.Windows.ToString(),
                        OSPlatform.OSX.ToString(),
                        OSPlatform.Linux.ToString()
                    },
                WorkerDirectory = string.Empty,
                Extensions = new List<string>() { ".py" },
                Language = "python"
            };
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _testSysRuntimeInfo, _testEnvironment);

            Assert.Equal(expectedPath, configFactory.GetHydratedWorkerPath(workerDescription));
        }

        [Theory]
        [InlineData("%FUNCTIONS_WORKER_RUNTIME_VERSION%/{os}/{architecture}", "3.6/LINUX/X64")]
        [InlineData("%FUNCTIONS_WORKER_RUNTIME_VERSION%/{architecture}", "3.6/X64")]
        [InlineData("%FUNCTIONS_WORKER_RUNTIME_VERSION%/{os}", "3.6/LINUX")]
        public void LanguageWorker_HydratedWorkerPath_EnvironmentVersionNotSet(string defaultWorkerPath, string expectedPath)
        {
            // We fall back to the default version when this is not set
            // Environment.SetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeVersionSettingName, "3.7");

            RpcWorkerDescription workerDescription = new RpcWorkerDescription()
            {
                Arguments = new List<string>(),
                DefaultExecutablePath = "python",
                SupportedArchitectures = new List<string>() { Architecture.X64.ToString(), Architecture.X86.ToString() },
                SupportedRuntimeVersions = new List<string>() { "3.6", "3.7" },
                SupportedOperatingSystems = new List<string>()
                    {
                        OSPlatform.Windows.ToString(),
                        OSPlatform.OSX.ToString(),
                        OSPlatform.Linux.ToString()
                    },
                DefaultWorkerPath = defaultWorkerPath,
                WorkerDirectory = string.Empty,
                Extensions = new List<string>() { ".py" },
                Language = "python",
                DefaultRuntimeVersion = "3.6"
            };
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _testSysRuntimeInfo, _testEnvironment);

            Assert.Equal(expectedPath, configFactory.GetHydratedWorkerPath(workerDescription));
        }

        [Theory]
        [InlineData(Architecture.Arm)]
        [InlineData(Architecture.Arm64)]
        public void LanguageWorker_HydratedWorkerPath_UnsupportedArchitecture(Architecture unsupportedArch)
        {
            RpcWorkerDescription workerDescription = new RpcWorkerDescription()
            {
                Arguments = new List<string>(),
                DefaultExecutablePath = "python",
                DefaultWorkerPath = "{architecture}/worker.py",
                WorkerDirectory = string.Empty,
                SupportedArchitectures = new List<string>() { Architecture.X64.ToString(), Architecture.X86.ToString() },
                SupportedRuntimeVersions = new List<string>() { "3.6", "3.7" },
                SupportedOperatingSystems = new List<string>()
                    {
                        OSPlatform.Windows.ToString(),
                        OSPlatform.OSX.ToString(),
                        OSPlatform.Linux.ToString()
                    },
                Extensions = new List<string>() { ".py" },
                Language = "python",
                DefaultRuntimeVersion = "3.7"
            };
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            Mock<ISystemRuntimeInformation> mockRuntimeInfo = new Mock<ISystemRuntimeInformation>();
            mockRuntimeInfo.Setup(r => r.GetOSArchitecture()).Returns(unsupportedArch);
            mockRuntimeInfo.Setup(r => r.GetOSPlatform()).Returns(OSPlatform.Linux);
            var configFactory = new WorkerConfigFactory(config, testLogger, mockRuntimeInfo.Object, _testEnvironment);

            var ex = Assert.Throws<PlatformNotSupportedException>(() => configFactory.GetHydratedWorkerPath(workerDescription));
            Assert.Equal(ex.Message, $"Architecture {unsupportedArch.ToString()} is not supported for language {workerDescription.Language}");
        }

        [Fact]
        public void LanguageWorker_HydratedWorkerPath_UnsupportedOS()
        {
            OSPlatform bogusOS = OSPlatform.Create("BogusOS");
            RpcWorkerDescription workerDescription = new RpcWorkerDescription()
            {
                Arguments = new List<string>(),
                DefaultExecutablePath = "python",
                DefaultWorkerPath = "{os}/worker.py",
                SupportedOperatingSystems = new List<string>()
                    {
                        OSPlatform.Windows.ToString(),
                        OSPlatform.OSX.ToString(),
                        OSPlatform.Linux.ToString()
                    },
                WorkerDirectory = string.Empty,
                Extensions = new List<string>() { ".py" },
                Language = "python",
                DefaultRuntimeVersion = "3.7"
            };
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            Mock<ISystemRuntimeInformation> mockRuntimeInfo = new Mock<ISystemRuntimeInformation>();
            mockRuntimeInfo.Setup(r => r.GetOSArchitecture()).Returns(Architecture.X64);
            mockRuntimeInfo.Setup(r => r.GetOSPlatform()).Returns(bogusOS);
            var configFactory = new WorkerConfigFactory(config, testLogger, mockRuntimeInfo.Object, _testEnvironment);

            var ex = Assert.Throws<PlatformNotSupportedException>(() => configFactory.GetHydratedWorkerPath(workerDescription));
            Assert.Equal(ex.Message, $"OS BogusOS is not supported for language {workerDescription.Language}");
        }

        [Fact]
        public void LanguageWorker_HydratedWorkerPath_UnsupportedDefaultRuntimeVersion()
        {
            RpcWorkerDescription workerDescription = new RpcWorkerDescription()
            {
                Arguments = new List<string>(),
                DefaultExecutablePath = "python",
                SupportedRuntimeVersions = new List<string>() { "3.6", "3.7" },
                DefaultWorkerPath = $"{LanguageWorkerConstants.RuntimeVersionPlaceholder}/worker.py",
                WorkerDirectory = string.Empty,
                Extensions = new List<string>() { ".py" },
                Language = "python",
                DefaultRuntimeVersion = "3.5"
            };
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _testSysRuntimeInfo, _testEnvironment);

            var ex = Assert.Throws<NotSupportedException>(() => configFactory.GetHydratedWorkerPath(workerDescription));
            Assert.Equal(ex.Message, $"Version {workerDescription.DefaultRuntimeVersion} is not supported for language {workerDescription.Language}");
        }

        [Fact]
        public void LanguageWorker_HydratedWorkerPath_UnsupportedEnvironmentRuntimeVersion()
        {
            _testEnvironment.SetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeVersionSettingName, "3.4");

            RpcWorkerDescription workerDescription = new RpcWorkerDescription()
            {
                Arguments = new List<string>(),
                DefaultExecutablePath = "python",
                SupportedRuntimeVersions = new List<string>() { "3.6", "3.7" },
                DefaultWorkerPath = $"{LanguageWorkerConstants.RuntimeVersionPlaceholder}/worker.py",
                WorkerDirectory = string.Empty,
                Extensions = new List<string>() { ".py" },
                Language = "python",
                DefaultRuntimeVersion = "3.7" // Ignore this if environment is set
            };
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                  .AddInMemoryCollection(new Dictionary<string, string>
                  {
                      ["languageWorker"] = "test"
                  });
            var config = configBuilder.Build();
            var scriptSettingsManager = new ScriptSettingsManager(config);
            var testLogger = new TestLogger("test");
            var configFactory = new WorkerConfigFactory(config, testLogger, _testSysRuntimeInfo, _testEnvironment);

            var ex = Assert.Throws<NotSupportedException>(() => configFactory.GetHydratedWorkerPath(workerDescription));
            Assert.Equal(ex.Message, $"Version 3.4 is not supported for language {workerDescription.Language}");
        }

        private IEnumerable<WorkerConfig> TestReadWorkerProviderFromConfig(IEnumerable<TestLanguageWorkerConfig> configs, ILogger testLogger, string language = null, Dictionary<string, string> keyValuePairs = null, bool appSvcEnv = false)
        {
            Mock<IEnvironment> mockEnvironment = new Mock<IEnvironment>();
            var workerPathSection = $"{LanguageWorkerConstants.LanguageWorkersSectionName}:{OutOfProcConstants.WorkersDirectorySectionName}";
            try
            {
                foreach (var workerConfig in configs)
                {
                    WorkerConfigTestUtilities.CreateWorkerFolder(rootPath, workerConfig);
                }

                IConfigurationRoot config = TestConfigBuilder(workerPathSection, keyValuePairs);

                var scriptHostOptions = new ScriptJobHostOptions();
                var scriptSettingsManager = new ScriptSettingsManager(config);
                var configFactory = new WorkerConfigFactory(config, testLogger, _testSysRuntimeInfo, _testEnvironment);
                if (appSvcEnv)
                {
                    var testEnvVariables = new Dictionary<string, string>
                    {
                        { EnvironmentSettingNames.AzureWebsiteInstanceId, "123" },
                    };
                    using (var variables = new TestScopedSettings(scriptSettingsManager, testEnvVariables))
                    {
                        configFactory.BuildWorkerProviderDictionary();
                        return configFactory.GetConfigs();
                    }
                }
                configFactory.BuildWorkerProviderDictionary();
                return configFactory.GetConfigs();
            }
            finally
            {
                WorkerConfigTestUtilities.DeleteTestDir(rootPath);
                WorkerConfigTestUtilities.DeleteTestDir(customRootPath);
            }
        }

        private static IConfigurationRoot TestConfigBuilder(string workerPathSection, Dictionary<string, string> keyValuePairs = null)
        {
            var configBuilderData = new Dictionary<string, string>
            {
                [workerPathSection] = rootPath
            };
            if (keyValuePairs != null)
            {
                configBuilderData.AddRange(keyValuePairs);
            }
            var configBuilder = ScriptSettingsManager.CreateDefaultConfigurationBuilder()
                                .AddInMemoryCollection(configBuilderData);
            var config = configBuilder.Build();
            return config;
        }

        private static TestLanguageWorkerConfig MakeTestConfig(string language, string[] arguments, bool invalid = false, string addAppSvcProfile = "", bool emptyWorkerPath = false)
        {
            string json = WorkerConfigTestUtilities.GetTestWorkerConfig(language, arguments, invalid, addAppSvcProfile, emptyWorkerPath).ToString();
            return new TestLanguageWorkerConfig()
            {
                Json = json,
                Language = language,
            };
        }
    }
}
