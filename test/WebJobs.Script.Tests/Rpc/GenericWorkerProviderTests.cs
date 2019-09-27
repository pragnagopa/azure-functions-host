﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Azure.WebJobs.Script.OutOfProc;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.Rpc
{
    public class GenericWorkerProviderTests
    {
        private static string rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static string customRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static string testLanguagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static string testLanguage = "testLanguage";

        public static IEnumerable<object[]> InvalidWorkerDescriptions
        {
            get
            {
                yield return new object[] { new WorkerDescription() { Extensions = new List<string>() } };
                yield return new object[] { new WorkerDescription() { Language = testLanguage } };
                yield return new object[] { new WorkerDescription() { Language = testLanguage, Extensions = new List<string>() } };
            }
        }

        public static IEnumerable<object[]> ValidWorkerDescriptions
        {
            get
            {
                yield return new object[] { new WorkerDescription() { Language = testLanguage, Extensions = new List<string>(), DefaultExecutablePath = "test.exe" } };
                yield return new object[] { new WorkerDescription() { Language = testLanguage, Extensions = new List<string>(), DefaultExecutablePath = "test.exe", DefaultWorkerPath = WorkerConfigTestUtilities.TestWorkerPathInWorkerConfig } };
                yield return new object[] { new WorkerDescription() { Language = testLanguage, Extensions = new List<string>(), DefaultExecutablePath = "test.exe", DefaultWorkerPath = WorkerConfigTestUtilities.TestWorkerPathInWorkerConfig, Arguments = new List<string>() } };
            }
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
            Assert.Equal(Path.Combine(rootPath, testLanguage, $"{WorkerConfigTestUtilities.TestWorkerPathInWorkerConfig}.{testLanguage}"), workerConfigs.Single().Description.GetWorkerPath());
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
            Assert.Equal(Path.Combine(rootPath, testLanguage, $"{WorkerConfigTestUtilities.TestWorkerPathInWorkerConfig}.{testLanguage}"), workerConfigs.Single().Description.GetWorkerPath());
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
            Assert.True(workerConfigs.Single().Description.GetWorkerPath() == null);
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
            Assert.Equal(Path.Combine(rootPath, testLanguage, $"{WorkerConfigTestUtilities.TestWorkerPathInWorkerConfig}.{testLanguage}"), workerConfig.Description.GetWorkerPath());
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
            Assert.Equal(Path.Combine(customRootPath, testLanguage, $"{WorkerConfigTestUtilities.TestWorkerPathInWorkerConfig}.{testLanguage}"), workerConfig.Description.GetWorkerPath());
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
            Assert.Throws<ValidationException>(() => workerDescription.ValidateRpcWorkerDescription());
        }

        [Theory]
        [MemberData(nameof(ValidWorkerDescriptions))]
        public void ValidateWorkerDescription_Succeeds(WorkerDescription workerDescription)
        {
            workerDescription.ValidateRpcWorkerDescription();
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
                var configFactory = new WorkerConfigFactory(config, testLogger);
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
                DeleteTestDir(rootPath);
                DeleteTestDir(customRootPath);
            }
        }

        private static void DeleteTestDir(string testDir)
        {
            if (Directory.Exists(testDir))
            {
                try
                {
                    Directory.Delete(testDir, true);
                }
                catch
                {
                    // best effort cleanup
                }
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
