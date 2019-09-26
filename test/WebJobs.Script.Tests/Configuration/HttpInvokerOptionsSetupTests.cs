// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.WebJobs.Script.Configuration;
using Microsoft.Azure.WebJobs.Script.OutOfProc.Http;
using Microsoft.Azure.WebJobs.Script.WebHost.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WebJobs.Script.Tests;
using Xunit;
using static Microsoft.Azure.WebJobs.Script.EnvironmentSettingNames;

namespace Microsoft.Azure.WebJobs.Script.Tests.Configuration
{
    public class HttpInvokerOptionsSetupTests
    {
        private readonly TestEnvironment _environment = new TestEnvironment();
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();
        private readonly string _hostJsonFile;
        private readonly string _rootPath;
        private readonly ScriptApplicationHostOptions _options;
        private ILoggerProvider _testLoggerProvider;
        private ILoggerFactory _testLoggerFactory;
        private static string _currentDirectory = Directory.GetCurrentDirectory();

        public HttpInvokerOptionsSetupTests()
        {
            _testLoggerProvider = new TestLoggerProvider();
            _testLoggerFactory = new LoggerFactory();
            _testLoggerFactory.AddProvider(_testLoggerProvider);
            _rootPath = Path.Combine(Environment.CurrentDirectory, "ScriptHostTests");
            Environment.SetEnvironmentVariable(AzureWebJobsScriptRoot, _rootPath);

            if (!Directory.Exists(_rootPath))
            {
                Directory.CreateDirectory(_rootPath);
            }

            _options = new ScriptApplicationHostOptions
            {
                ScriptPath = _rootPath
            };

            _hostJsonFile = Path.Combine(_rootPath, "host.json");
            if (File.Exists(_hostJsonFile))
            {
                File.Delete(_hostJsonFile);
            }
        }

        [Theory]
        [InlineData(@"{
                    'version': '2.0',
                    }")]
        [InlineData(@"{
                    'version': '2.0',
                    'httpInvoker': {
                            'description': {
                                'defaultExecutablePath': 'testExe'
                            }
                        }
                    }")]
        public void MissingOrValid_HttpInvokerConfig_DoesNotThrowException(string hostJsonContent)
        {
            File.WriteAllText(_hostJsonFile, hostJsonContent);
            var configuration = BuildHostJsonConfiguration();
            HttpInvokerOptionsSetup setup = new HttpInvokerOptionsSetup(configuration, _testLoggerFactory);
            HttpInvokerOptions options = new HttpInvokerOptions();
            var ex = Record.Exception(() => setup.Configure(options));
            Assert.Null(ex);
            if (!string.IsNullOrEmpty(options.Description.DefaultExecutablePath))
            {
                string expectedDefaultExecutablePath = Path.Combine(Directory.GetCurrentDirectory(), "textExe");
                Assert.Equal(expectedDefaultExecutablePath, options.Description.DefaultExecutablePath);
            }
        }

        [Theory]
        [InlineData(@"{
                    'version': '2.0',
                    'httpInvoker': {
                            'description': {
                                'arguments': ['--xTest1 --xTest2'],
                                'defaultExecutablePath': 'c:\testPath\testExe'
                            }
                        }
                    }")]
        public void HttpInvoker_Custom_WorkerDirectory_Arguments(string hostJsonContent)
        {
            File.WriteAllText(_hostJsonFile, hostJsonContent);
            var configuration = BuildHostJsonConfiguration();
            HttpInvokerOptionsSetup setup = new HttpInvokerOptionsSetup(configuration, _testLoggerFactory);
            HttpInvokerOptions options = new HttpInvokerOptions();
            setup.Configure(options);
            Assert.Equal("c:\testPath\testExe", options.Description.DefaultExecutablePath);
            Assert.Equal(1, options.Description.Arguments.Count);
            Assert.Equal("--xTest1 --xTest2", options.Description.Arguments[0]);
        }

        [Theory]
        [InlineData(@"{
                    'version': '2.0',
                    'httpInvoker': {
                            'description': {
                                'arguments': ['--xTest1 --xTest2'],
                                'defaultExecutablePath': 'node',
                                'defaultWorkerPath': 'httpInvoker.js'
                            }
                        }
                    }", false, true, true)]
        [InlineData(@"{
                    'version': '2.0',
                    'httpInvoker': {
                            'description': {
                                'arguments': ['--xTest1 --xTest2'],
                                'defaultExecutablePath': 'node'
                            }
                        }
                    }", true, false, false)]
        [InlineData(@"{
                    'version': '2.0',
                    'httpInvoker': {
                            'description': {
                                'arguments': ['--xTest1 --xTest2'],
                                'defaultExecutablePath': 'c:/myruntime/node'
                            }
                        }
                    }", false, false, false)]
        [InlineData(@"{
                    'version': '2.0',
                    'httpInvoker': {
                            'description': {
                                'arguments': ['--xTest1 --xTest2'],
                                'defaultExecutablePath': 'c:/myruntime/node',
                                'defaultWorkerPath': 'c:/myworkerPath/httpInvoker.js'
                            }
                        }
                    }", false, false, true)]
        public void HttpInvoker_Config_ExpectedValues(string hostJsonContent, bool appendCurrentDirectoryToExe, bool appendCurrentDirToWorkerPath, bool workerPathSet)
        {
            File.WriteAllText(_hostJsonFile, hostJsonContent);
            var configuration = BuildHostJsonConfiguration();
            HttpInvokerOptionsSetup setup = new HttpInvokerOptionsSetup(configuration, _testLoggerFactory);
            HttpInvokerOptions options = new HttpInvokerOptions();
            setup.Configure(options);

            //Verify worker exe path is expected
            if (appendCurrentDirectoryToExe)
            {
                Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "node"), options.Description.DefaultExecutablePath);
            }
            else if (Path.IsPathRooted(options.Description.DefaultExecutablePath))
            {
                Assert.Equal(@"c:/myruntime/node", options.Description.DefaultExecutablePath);
            }
            else
            {
                Assert.Equal("node", options.Description.DefaultExecutablePath);
            }

            //Verify worker path is expected
            if (appendCurrentDirToWorkerPath)
            {
                Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "httpInvoker.js"), options.Description.DefaultWorkerPath);
            }
            else if (!workerPathSet)
            {
                Assert.Null(options.Description.DefaultWorkerPath);
            }
            else
            {
                Assert.Equal(@"c:/myworkerPath/httpInvoker.js", options.Description.DefaultWorkerPath);
            }

            Assert.Equal(1, options.Description.Arguments.Count);
            Assert.Equal("--xTest1 --xTest2", options.Description.Arguments[0]);
        }

        private IConfiguration BuildHostJsonConfiguration(IEnvironment environment = null)
        {
            environment = environment ?? new TestEnvironment();

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);

            var configSource = new HostJsonFileConfigurationSource(_options, environment, loggerFactory);

            var configurationBuilder = new ConfigurationBuilder()
                .Add(configSource);

            return configurationBuilder.Build();
        }
    }
}
