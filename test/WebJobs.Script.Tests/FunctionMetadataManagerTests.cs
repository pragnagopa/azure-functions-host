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
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\queueTrigger.py", new MockFileData(string.Empty) },
                { @"c:\functions\helper.py", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);
            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(scriptFileName, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\queueTrigger.py", scriptFile, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_RelativeSourceFileSpecified()
        {
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\shared\queueTrigger.py", new MockFileData(string.Empty) },
                { @"c:\functions\queueTrigger.py", new MockFileData(string.Empty) },
                { @"c:\functions\helper.py", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);
            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(@"..\shared\queuetrigger.py", @"c:\functions", fileSystem);
            Assert.Equal(@"c:\shared\queueTrigger.py", scriptFile, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_MultipleFiles_NoClearPrimary_Throws()
        {
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\foo.py", new MockFileData(string.Empty) },
                { @"c:\functions\queueTrigger.py", new MockFileData(string.Empty) },
                { @"c:\functions\helper.py", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);
            Assert.Throws<FunctionConfigurationException>(() => _testFunctionMetadataManager.DeterminePrimaryScriptFile(string.Empty, @"c:\functions", fileSystem));
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
            Assert.Null(testFunctionMetadataManager.DeterminePrimaryScriptFile(null, @"c:\functions", fileSystem));
        }

        [Fact]
        public void DeterminePrimaryScriptFile_NoFiles_Throws()
        {
            string[] functionFiles = new string[0];

            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory(@"c:\functions");

            Assert.Throws<FunctionConfigurationException>(() => _testFunctionMetadataManager.DeterminePrimaryScriptFile("run.csx", @"c:\functions", fileSystem));
        }

        [Fact]
        public void DeterminePrimaryScriptFile_NoFiles_HttpWorker_Returns_Null()
        {
            string[] functionFiles = new string[0];

            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory(@"c:\functions");

            FunctionMetadataManager testFunctionMetadataManager = new FunctionMetadataManager(new OptionsWrapper<ScriptJobHostOptions>(_scriptJobHostOptions), _mockFunctionMetadataProvider.Object, new OptionsWrapper<LanguageWorkerOptions>(GetTestLanguageWorkerOptions()), new OptionsWrapper<HttpWorkerOptions>(GetTestHttpWorkerOptions()), MockNullLoggerFactory.CreateLoggerFactory());
            Assert.Null(testFunctionMetadataManager.DeterminePrimaryScriptFile(null, @"c:\functions", fileSystem));
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

            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(null, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\Run.csx", scriptFile);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_SingleFile()
        {
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\Run.csx", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);

            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(null, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\Run.csx", scriptFile);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_MultipleFiles_RunTrumpsIndex()
        {
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\run.js", new MockFileData(string.Empty) },
                { @"c:\functions\index.js", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);

            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile(string.Empty, @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\run.js", scriptFile);
        }

        [Fact]
        public void DeterminePrimaryScriptFile_MultipleFiles_IndexFilePresent()
        {
            var files = new Dictionary<string, MockFileData>
            {
                { @"c:\functions\index.js", new MockFileData(string.Empty) },
                { @"c:\functions\test.txt", new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);

            string scriptFile = _testFunctionMetadataManager.DeterminePrimaryScriptFile("index.js", @"c:\functions", fileSystem);
            Assert.Equal(@"c:\functions\index.js", scriptFile);
        }

        [Theory]
        [InlineData("run.py", @"c:\functions\run.py")]
        [InlineData("queueTrigger.py", @"c:\functions\queueTrigger.py")]
        [InlineData("helper.py", @"c:\functions\helper.py")]
        [InlineData("test.txt", @"c:\functions\test.txt")]
        public void DeterminePrimaryScriptFile_MultipleFiles_ConfigTrumpsConvention(string scriptFileProperty, string expedtedScriptFilePath)
        {
            var files = new Dictionary<string, MockFileData>
            {
                { expedtedScriptFilePath, new MockFileData(string.Empty) }
            };
            var fileSystem = new MockFileSystem(files);

            string actualScriptFilePath = _testFunctionMetadataManager.DeterminePrimaryScriptFile(scriptFileProperty, @"c:\functions", fileSystem);
            Assert.Equal(expedtedScriptFilePath, actualScriptFilePath);
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
