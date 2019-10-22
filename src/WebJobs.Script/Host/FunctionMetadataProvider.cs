﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Diagnostics.Extensions;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Script
{
    public class FunctionMetadataProvider : IFunctionMetadataProvider
    {
        private readonly IOptionsMonitor<ScriptApplicationHostOptions> _applicationHostOptions;
        private readonly IMetricsLogger _metricsLogger;
        private readonly IEnumerable<WorkerConfig> _workerConfigs;
        private readonly Dictionary<string, ICollection<string>> _functionErrors = new Dictionary<string, ICollection<string>>();
        private readonly ILogger _logger;
        private ImmutableArray<FunctionMetadata> _functions;

        public FunctionMetadataProvider(IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions, IOptions<LanguageWorkerOptions> languageWorkerOptions, ILogger<FunctionMetadataProvider> logger, IMetricsLogger metricsLogger)
        {
            _applicationHostOptions = applicationHostOptions;
            _metricsLogger = metricsLogger;
            _logger = logger;
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
        }

        public ImmutableDictionary<string, ImmutableArray<string>> FunctionErrors
           => _functionErrors.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value.ToImmutableArray());

        public ImmutableArray<FunctionMetadata> GetFunctionMetadata(bool forceRefresh)
        {
            if (_functions.IsDefaultOrEmpty || forceRefresh)
            {
                _logger.FunctionMetadataProviderParsingFunctions();
                Collection<FunctionMetadata> functionMetadata = ReadFunctionsMetadata();
                _logger.FunctionMetadataProviderFunctionFound(functionMetadata.Count);
                _functions = functionMetadata.ToImmutableArray();
            }

            return _functions;
        }

        internal Collection<FunctionMetadata> ReadFunctionsMetadata(IFileSystem fileSystem = null)
        {
            _functionErrors.Clear();
            fileSystem = fileSystem ?? FileUtility.Instance;
            using (_metricsLogger.LatencyEvent(MetricEventNames.ReadFunctionsMetadata))
            {
                var functions = new Collection<FunctionMetadata>();

                if (!fileSystem.Directory.Exists(_applicationHostOptions.CurrentValue.ScriptPath))
                {
                    return functions;
                }

                var functionDirectories = fileSystem.Directory.EnumerateDirectories(_applicationHostOptions.CurrentValue.ScriptPath).ToImmutableArray();
                foreach (var functionDirectory in functionDirectories)
                {
                    var function = ReadFunctionMetadata(functionDirectory, fileSystem);
                    if (function != null)
                    {
                        functions.Add(function);
                    }
                }
                return functions;
            }
        }

        private FunctionMetadata ReadFunctionMetadata(string functionDirectory, IFileSystem fileSystem)
        {
            using (_metricsLogger.LatencyEvent(string.Format(MetricEventNames.ReadFunctionMetadata, functionDirectory)))
            {
                string functionName = null;

                try
                {
                    // read the function config
                    if (!Utility.TryReadFunctionConfig(functionDirectory, out string json, fileSystem))
                    {
                        // not a function directory
                        return null;
                    }

                    functionName = Path.GetFileName(functionDirectory);

                    ValidateName(functionName);

                    JObject functionConfig = JObject.Parse(json);

                    return ParseFunctionMetadata(functionName, functionConfig, functionDirectory);
                }
                catch (Exception ex)
                {
                    // log any unhandled exceptions and continue
                    Utility.AddFunctionError(_functionErrors, functionName, Utility.FlattenException(ex, includeSource: false), isFunctionShortName: true);
                }
                return null;
            }
        }

        internal void ValidateName(string name, bool isProxy = false)
        {
            if (!Utility.IsValidFunctionName(name))
            {
                throw new InvalidOperationException(string.Format("'{0}' is not a valid {1} name.", name, isProxy ? "proxy" : "function"));
            }
        }

        private FunctionMetadata ParseFunctionMetadata(string functionName, JObject configMetadata, string scriptDirectory)
        {
            var functionMetadata = new FunctionMetadata
            {
                Name = functionName,
                FunctionDirectory = scriptDirectory
            };

            JArray bindingArray = (JArray)configMetadata["bindings"];
            if (bindingArray == null || bindingArray.Count == 0)
            {
                throw new FormatException("At least one binding must be declared.");
            }

            if (bindingArray != null)
            {
                foreach (JObject binding in bindingArray)
                {
                    BindingMetadata bindingMetadata = BindingMetadata.Create(binding);
                    functionMetadata.Bindings.Add(bindingMetadata);
                }
            }

            JToken isDirect;
            if (configMetadata.TryGetValue("configurationSource", StringComparison.OrdinalIgnoreCase, out isDirect))
            {
                var isDirectValue = isDirect.ToString();
                if (string.Equals(isDirectValue, "attributes", StringComparison.OrdinalIgnoreCase))
                {
                    functionMetadata.IsDirect = true;
                }
                else if (!string.Equals(isDirectValue, "config", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException($"Illegal value '{isDirectValue}' for 'configurationSource' property in {functionMetadata.Name}'.");
                }
            }
            functionMetadata.Language = ParseLanguage(functionMetadata.ScriptFile);
            return functionMetadata;
        }

        internal string ParseLanguage(string scriptFilePath)
        {
            // scriptFilePath is not required for HttpWorker
            if (string.IsNullOrEmpty(scriptFilePath))
            {
                return null;
            }

            // determine the script type based on the primary script file extension
            string extension = Path.GetExtension(scriptFilePath).ToLowerInvariant().TrimStart('.');
            switch (extension)
            {
                case "csx":
                case "cs":
                    return DotNetScriptTypes.CSharp;
                case "dll":
                    return DotNetScriptTypes.DotNetAssembly;
            }
            var workerConfig = _workerConfigs.FirstOrDefault(config => config.Description.Extensions.Contains("." + extension));
            if (workerConfig != null)
            {
                return workerConfig.Description.Language;
            }
            return null;
        }
    }
}