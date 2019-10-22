// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests
{
    public class FunctionMetadataProviderTests
    {
        private TestMetricsLogger _testMetricsLogger;
        private ScriptApplicationHostOptions _scriptApplicationHostOptions;

        public FunctionMetadataProviderTests()
        {
            _testMetricsLogger = new TestMetricsLogger();
            _scriptApplicationHostOptions = new ScriptApplicationHostOptions();
        }

        [Fact]
        public void ReadFunctionMetadata_Succeeds()
        {
            string functionsPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\..\sample\node");
            _scriptApplicationHostOptions.ScriptPath = functionsPath;
            var optionsMonitor = TestHelpers.CreateOptionsMonitor(_scriptApplicationHostOptions);
            var metadataProvider = new FunctionMetadataProvider(optionsMonitor, NullLogger<FunctionMetadataProvider>.Instance, _testMetricsLogger);

            Assert.Equal(17, metadataProvider.GetFunctionMetadata(false).Length);
            Assert.True(AreRequiredMetricsEmitted(_testMetricsLogger));
        }

        private bool AreRequiredMetricsEmitted(TestMetricsLogger metricsLogger)
        {
            bool hasBegun = false;
            bool hasEnded = false;
            foreach (string begin in metricsLogger.EventsBegan)
            {
                if (begin.Contains(MetricEventNames.ReadFunctionMetadata.Substring(0, MetricEventNames.ReadFunctionMetadata.IndexOf('{'))))
                {
                    hasBegun = true;
                    break;
                }
            }
            foreach (string end in metricsLogger.EventsEnded)
            {
                if (end.Contains(MetricEventNames.ReadFunctionMetadata.Substring(0, MetricEventNames.ReadFunctionMetadata.IndexOf('{'))))
                {
                    hasEnded = true;
                    break;
                }
            }
            return hasBegun && hasEnded && (metricsLogger.EventsBegan.Contains(MetricEventNames.ReadFunctionsMetadata)
                && metricsLogger.EventsEnded.Contains(MetricEventNames.ReadFunctionsMetadata));
        }

        [Theory]
        [InlineData("")]
        [InlineData("host")]
        [InlineData("Host")]
        [InlineData("-function")]
        [InlineData("_function")]
        [InlineData("function test")]
        [InlineData("function.test")]
        [InlineData("function0.1")]
        public void ValidateFunctionName_ThrowsOnInvalidName(string functionName)
        {
            string functionsPath = "c:\testdir";
            _scriptApplicationHostOptions.ScriptPath = functionsPath;
            var optionsMonitor = TestHelpers.CreateOptionsMonitor(_scriptApplicationHostOptions);
            var metadataProvider = new FunctionMetadataProvider(optionsMonitor, NullLogger<FunctionMetadataProvider>.Instance, _testMetricsLogger);

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                metadataProvider.ValidateName(functionName);
            });

            Assert.Equal(string.Format("'{0}' is not a valid function name.", functionName), ex.Message);
        }

        [Theory]
        [InlineData("testwithhost")]
        [InlineData("hosts")]
        [InlineData("myfunction")]
        [InlineData("myfunction-test")]
        [InlineData("myfunction_test")]
        public void ValidateFunctionName_DoesNotThrowOnValidName(string functionName)
        {
            string functionsPath = "c:\testdir";
            _scriptApplicationHostOptions.ScriptPath = functionsPath;
            var optionsMonitor = TestHelpers.CreateOptionsMonitor(_scriptApplicationHostOptions);
            var metadataProvider = new FunctionMetadataProvider(optionsMonitor, NullLogger<FunctionMetadataProvider>.Instance, _testMetricsLogger);

            try
            {
                metadataProvider.ValidateName(functionName);
            }
            catch (InvalidOperationException)
            {
                Assert.True(false, $"Valid function name {functionName} failed validation.");
            }
        }
    }
}
