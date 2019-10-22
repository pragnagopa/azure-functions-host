// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Azure.WebJobs.Script.OutOfProc.Http;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests
{
    public class FunctionMetadataManagerTests
    {
        private ScriptJobHostOptions _scriptJobHostOptions = new ScriptJobHostOptions();
        private Mock<IFunctionMetadataProvider> _mockFunctionMetadataProvider;
        private FunctionMetadataManager _testFunctionMetadataManager;

        public FunctionMetadataManagerTests()
        {
            _mockFunctionMetadataProvider = new Mock<IFunctionMetadataProvider>();
            string functionsPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\..\sample\node");
            HttpWorkerOptions defaultHttpWorkerOptions = new HttpWorkerOptions();
            _scriptJobHostOptions.RootScriptPath = functionsPath;
            _testFunctionMetadataManager = new FunctionMetadataManager(new OptionsWrapper<ScriptJobHostOptions>(_scriptJobHostOptions), _mockFunctionMetadataProvider.Object, new OptionsWrapper<LanguageWorkerOptions>(GetTestLanguageWorkerOptions()), new OptionsWrapper<HttpWorkerOptions>(defaultHttpWorkerOptions), MockNullLoggerFactory.CreateLoggerFactory());
        }

        [Theory]
        [InlineData("QUEUETriggER.py")]
        [InlineData("queueTrigger.py")]
        public void DeterminePrimaryScriptFile_MultipleFiles_SourceFileSpecified(string scriptFileName)
        {
            JObject functionConfig = new JObject()
            {
                { "scriptFile", scriptFileName }
            };

            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\queueTrigger.py", new MockFileData(string.Empty) },
                { @"c:\functions\helper.py", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);
            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\queueTrigger.py", scriptFile, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_RelativeSourceFileSpecified()
        {
            JObject functionConfig = new JObject()
            {
                { "scriptFile", @"..\shared\queuetrigger.py" }
            };

            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\shared\queueTrigger.py", new MockFileData(string.Empty) },
                { @"c:\functions\queueTrigger.py", new MockFileData(string.Empty) },
                { @"c:\functions\helper.py", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);
            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\shared\queueTrigger.py", scriptFile, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_MultipleFiles_NoClearPrimary_Throws()
        {
            var functionConfig = new JObject();
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\foo.py", new MockFileData(string.Empty) },
                { @"c:\functions\queueTrigger.py", new MockFileData(string.Empty) },
                { @"c:\functions\helper.py", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);
            Assert.Throws<FunctionConfigurationException>(() => _testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem));
        }

        [Fact]
        public void DeterminePrimaryScriptFile_MultipleFiles_NoClearPrimary_HttpWorker_Returns_Null()
        {
            var functionConfig = new JObject();
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\foo.py", new MockFileData(string.Empty) },
                { @"c:\functions\queueTrigger.py", new MockFileData(string.Empty) },
                { @"c:\functions\helper.py", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);

            FunctionMetadataManager testFunctionMetadataManager = new FunctionMetadataManager(new OptionsWrapper<ScriptJobHostOptions>(_scriptJobHostOptions), _mockFunctionMetadataProvider.Object, new OptionsWrapper<LanguageWorkerOptions>(GetTestLanguageWorkerOptions()), new OptionsWrapper<HttpWorkerOptions>(GetTestHttpWorkerOptions()), MockNullLoggerFactory.CreateLoggerFactory());
            Assert.Null(testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem));
        }

        [Fact]
        public void DeterminePrimaryScriptFile_NoFiles_Throws()
        {
            var functionConfig = new JObject();
            string[] functionFiles = new string[0];

            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory(@"c:\functions");

            Assert.Throws<FunctionConfigurationException>(() => _testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem));
        }

        [Fact]
        public void DeterminePrimaryScriptFile_NoFiles_HttpWorker_Returns_Null()
        {
            var functionConfig = new JObject();
            string[] functionFiles = new string[0];

            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory(@"c:\functions");

            FunctionMetadataManager testFunctionMetadataManager = new FunctionMetadataManager(new OptionsWrapper<ScriptJobHostOptions>(_scriptJobHostOptions), _mockFunctionMetadataProvider.Object, new OptionsWrapper<LanguageWorkerOptions>(GetTestLanguageWorkerOptions()), new OptionsWrapper<HttpWorkerOptions>(GetTestHttpWorkerOptions()), MockNullLoggerFactory.CreateLoggerFactory());
            Assert.Null(testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem));
        }

        [Fact]
        public void DeterminePrimaryScriptFile_MultipleFiles_RunFilePresent()
        {
            var functionConfig = new JObject();
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\Run.csx", new MockFileData(string.Empty) },
                { @"c:\functions\Helper.csx", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);

            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\Run.csx", scriptFile);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_SingleFile()
        {
            var functionConfig = new JObject();
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\Run.csx", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);

            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\Run.csx", scriptFile);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_MultipleFiles_RunTrumpsIndex()
        {
            var functionConfig = new JObject();
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\run.js", new MockFileData(string.Empty) },
                { @"c:\functions\index.js", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);

            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\run.js", scriptFile);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_MultipleFiles_IndexFilePresent()
        {
            var functionConfig = new JObject();
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\index.js", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);

            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\index.js", scriptFile);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_MultipleFiles_ConfigTrumpsConvention()
        {
            JObject functionConfig = new JObject()
            {
                { "scriptFile", "queueTrigger.py" }
            };
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\run.py", new MockFileData(string.Empty) },
                { @"c:\functions\queueTrigger.py", new MockFileData(string.Empty) },
                { @"c:\functions\helper.py", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);

            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(functionConfig, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\queueTrigger.py", scriptFile);
        }

        [Theory]
        [InlineData("node", "test.js")]
        [InlineData("java", "test.jar")]
        [InlineData("CSharp", "test.cs")]
        [InlineData("CSharp", "test.csx")]
        [InlineData("DotNetAssembly", "test.dll")]
        [InlineData(null, "test.x")]
        public void ParseLanguage_Returns_ExpectedLanguage(string language, string scriptFile)
        {
            Assert.Equal(language, _testFunctionMetadataManager.ParseLanguage(scriptFile));
        }

        [Theory]
        [InlineData("test.js")]
        [InlineData("test.jar")]
        [InlineData("test.x")]
        [InlineData("test.py")]
        [InlineData("")]
        [InlineData(null)]
        public void ParseLanguage_HttpWorker_Returns_Null(string scriptFile)
        {
            var workerOptions = new LanguageWorkerOptions
            {
                WorkerConfigs = TestHelpers.GetTestWorkerConfigsNoLanguage()
            };
            FunctionMetadataManager testFunctionMetadataManager = new FunctionMetadataManager(new OptionsWrapper<ScriptJobHostOptions>(_scriptJobHostOptions), _mockFunctionMetadataProvider.Object, new OptionsWrapper<LanguageWorkerOptions>(workerOptions), new OptionsWrapper<HttpWorkerOptions>(GetTestHttpWorkerOptions()), MockNullLoggerFactory.CreateLoggerFactory());
            Assert.Equal(null, testFunctionMetadataManager.ParseLanguage(scriptFile));
        }

        private static HttpWorkerOptions GetTestHttpWorkerOptions()
        {
            return new HttpWorkerOptions()
            {
                Description = new HttpWorkerDescription()
                {
                    DefaultExecutablePath = "text.exe"
                }
            };
        }

        private static LanguageWorkerOptions GetTestLanguageWorkerOptions()
        {
            return new LanguageWorkerOptions
            {
                WorkerConfigs = TestHelpers.GetTestWorkerConfigs()
            };
        }
    }
}
