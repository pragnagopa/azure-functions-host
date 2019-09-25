// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc.Http
{
    internal class HttpInvokerOptionsSetup : IConfigureOptions<HttpInvokerOptions>
    {
        private IConfiguration _configuration;
        private ILogger _logger;

        public HttpInvokerOptionsSetup(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<HttpInvokerOptionsSetup>();
        }

        public void Configure(HttpInvokerOptions options)
        {
            IConfigurationSection jobHostSection = _configuration.GetSection(ConfigurationSectionNames.JobHost);
            var httpInvokerSection = jobHostSection.GetSection(ConfigurationSectionNames.HttpInvoker);
            httpInvokerSection.Bind(options);
            ValidateWorkerDescription(options.Description);
            var arguments = new WorkerProcessArguments()
            {
                ExecutablePath = options.Description.DefaultExecutablePath,
                WorkerPath = options.Description.GetWorkerPath()
            };

            var workerProvider = new GenericWorkerProvider(options.Description, options.Description.WorkerDirectory);
            if (workerProvider.TryConfigureArguments(arguments, _logger))
            {
                _logger.LogError("Configured httpInvoker with DefaultExecutalbePath: {exepath} with arguments {args}", options.Description.DefaultExecutablePath, options.Arguments);
                options.Arguments = arguments;
            }
            else
            {
                _logger.LogError("Could not configure httpInvoker with DefaultExecutalbePath: {exepath}", options.Description.DefaultExecutablePath);
            }
        }

        internal void ValidateWorkerDescription(WorkerDescription workerDescription)
        {
            try
            {
                if (workerDescription != null)
                {
                    string workerPath = workerDescription.GetWorkerPath();
                    workerDescription.WorkerDirectory = workerDescription.WorkerDirectory ?? Directory.GetCurrentDirectory();
                    workerDescription.Arguments = workerDescription.Arguments ?? new List<string>();
                    if (string.IsNullOrEmpty(workerDescription.DefaultWorkerPath))
                    {
                        workerDescription.DefaultExecutablePath = Path.Combine(workerDescription.WorkerDirectory, workerDescription.DefaultExecutablePath);
                    }
                    workerDescription.ValidateHttpInvokerDescription();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to initialize HttpInvoker worker provider for");
            }
        }
    }
}
