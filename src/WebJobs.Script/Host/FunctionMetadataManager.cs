// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Diagnostics.Extensions;
using Microsoft.Azure.WebJobs.Script.OutOfProc.Http;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Script
{
    public class FunctionMetadataManager : IFunctionMetadataManager
    {
        private readonly IEnumerable<WorkerConfig> _workerConfigs;
        private readonly bool _isHttpWorker;
        private readonly Lazy<ImmutableArray<FunctionMetadata>> _functionMetadataArray;
        private readonly IOptions<ScriptJobHostOptions> _scriptOptions;
        private readonly IFunctionMetadataProvider _functionMetadataProvider;
        private readonly ILogger _logger;
        private Dictionary<string, ICollection<string>> _functionErrors = new Dictionary<string, ICollection<string>>();

        public FunctionMetadataManager(IOptions<ScriptJobHostOptions> scriptOptions, IFunctionMetadataProvider functionMetadataProvider, IOptions<LanguageWorkerOptions> languageWorkerOptions, IOptions<HttpWorkerOptions> httpWorkerOptions, ILoggerFactory loggerFactory)
        {
            _scriptOptions = scriptOptions;
            _functionMetadataProvider = functionMetadataProvider;
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _logger = loggerFactory.CreateLogger(LogCategories.Startup);
            _functionMetadataArray = new Lazy<ImmutableArray<FunctionMetadata>>(LoadFunctionMetadata);
            _isHttpWorker = httpWorkerOptions.Value.Description != null;
        }

        public ImmutableArray<FunctionMetadata> Functions => _functionMetadataArray.Value;

        public ImmutableDictionary<string, ImmutableArray<string>> Errors { get; private set; }

        /// <summary>
        /// Read all functions and populate function metadata.
        /// </summary>
        private ImmutableArray<FunctionMetadata> LoadFunctionMetadata()
        {
            ICollection<string> functionsWhiteList = _scriptOptions.Value.Functions;
            _logger.FunctionMetadataManagerLoadingFunctionsMetadata();
            List<FunctionMetadata> functionMetadataList = _functionMetadataProvider.GetFunctionMetadata().ToList();
            List<FunctionMetadata> validFunctionMetadataList = _functionMetadataProvider.GetFunctionMetadata().ToList();
            foreach (var error in _functionMetadataProvider.FunctionErrors)
            {
                ICollection<string> functionErrors = new Collection<string>(error.Value.ToList());
                _functionErrors.Add(error.Key, functionErrors);
            }
            // Validate
            foreach (FunctionMetadata functionMetadata in functionMetadataList)
            {
                if (!TryParseFunctionMetadata(functionMetadata, out string functionError))
                {
                    // for functions in error, log the error and don't
                    // add to the functions collection
                    Utility.AddFunctionError(_functionErrors, functionMetadata.Name, functionError);
                    validFunctionMetadataList.Remove(functionMetadata);
                }
            }
            Errors = _functionErrors.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value.ToImmutableArray());

            if (functionsWhiteList != null)
            {
                _logger.LogInformation($"A function whitelist has been specified, excluding all but the following functions: [{string.Join(", ", functionsWhiteList)}]");
                validFunctionMetadataList = validFunctionMetadataList.Where(function => functionsWhiteList.Any(functionName => functionName.Equals(function.Name, StringComparison.CurrentCultureIgnoreCase))).ToList();
                Errors = _functionErrors.Where(kvp => functionsWhiteList.Any(functionName => functionName.Equals(kvp.Key, StringComparison.CurrentCultureIgnoreCase))).ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value.ToImmutableArray());
            }
            _logger.FunctionMetadataManagerFunctionsLoaded(functionMetadataList.Count());

            // Exclude invalid functions
            return validFunctionMetadataList.ToImmutableArray();
        }

        private bool TryParseFunctionMetadata(FunctionMetadata functionMetadata, out string error)
        {
            error = null;
            try
            {
                functionMetadata.ScriptFile = DeterminePrimaryScriptFile(functionMetadata.ScriptFile, functionMetadata.FunctionDirectory);
            }
            catch (FunctionConfigurationException exc)
            {
                error = exc.Message;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Determines which script should be considered the "primary" entry point script.
        /// </summary>
        /// <exception cref="ConfigurationErrorsException">Thrown if the function metadata points to an invalid script file, or no script files are present for C# functions</exception>
        internal string DeterminePrimaryScriptFile(string scriptFile, string scriptDirectory, IFileSystem fileSystem = null)
        {
            fileSystem = fileSystem ?? FileUtility.Instance;

            // First see if there is an explicit primary file indicated
            // in config. If so use that.
            string functionPrimary = null;

            if (!string.IsNullOrEmpty(scriptFile))
            {
                string scriptPath = fileSystem.Path.Combine(scriptDirectory, scriptFile);
                if (!_isHttpWorker && !fileSystem.File.Exists(scriptPath))
                {
                    throw new FunctionConfigurationException("Invalid script file name configuration. The 'scriptFile' property is set to a file that does not exist.");
                }

                functionPrimary = scriptPath;
            }
            else
            {
                string[] functionFiles = fileSystem.Directory.EnumerateFiles(scriptDirectory)
                    .Where(p => fileSystem.Path.GetFileName(p).ToLowerInvariant() != ScriptConstants.FunctionMetadataFileName)
                    .ToArray();

                if (functionFiles.Length == 0 && !_isHttpWorker)
                {
                    throw new FunctionConfigurationException("No function script files present.");
                }

                if (functionFiles.Length == 1)
                {
                    // if there is only a single file, that file is primary
                    functionPrimary = functionFiles[0];
                }
                else
                {
                    // if there is a "run" file, that file is primary,
                    // for Node, any index.js file is primary
                    functionPrimary = functionFiles.FirstOrDefault(p =>
                        fileSystem.Path.GetFileNameWithoutExtension(p).ToLowerInvariant() == "run" ||
                        fileSystem.Path.GetFileName(p).ToLowerInvariant() == "index.js");
                }
            }

            if (!_isHttpWorker && string.IsNullOrEmpty(functionPrimary))
            {
                throw new FunctionConfigurationException("Unable to determine the primary function script. Try renaming your entry point script to 'run' (or 'index' in the case of Node), " +
                    "or alternatively you can specify the name of the entry point script explicitly by adding a 'scriptFile' property to your function metadata.");
            }

            // Primary script file is not required for HttpWorker or any custom language worker
            if (string.IsNullOrEmpty(functionPrimary))
            {
                return null;
            }
            return Path.GetFullPath(functionPrimary);
        }
    }
}