// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
            if (httpInvokerSection.Exists())
            {
                httpInvokerSection.Bind(options);

                if (options.Description == null || string.IsNullOrEmpty(options.Description.DefaultExecutablePath))
                {
                    throw new HostConfigurationException($"Invalid WorkerDescription for HttpInvoker");
                }

                ValidateWorkerDescription(options.Description);
                var arguments = new WorkerProcessArguments()
                {
                    ExecutablePath = options.Description.DefaultExecutablePath,
                    WorkerPath = options.Description.DefaultWorkerPath
                };

                arguments.ExecutableArguments.AddRange(options.Description.Arguments);
                _logger.LogDebug("Configured httpInvoker with DefaultExecutalbePath: {exepath} with arguments {args}", options.Description.DefaultExecutablePath, options.Arguments);
            }
        }

        internal void ValidateWorkerDescription(WorkerDescription workerDescription)
        {
            if (workerDescription != null)
            {
                workerDescription.WorkerDirectory = workerDescription.WorkerDirectory ?? Directory.GetCurrentDirectory();
                workerDescription.Arguments = workerDescription.Arguments ?? new List<string>();
                workerDescription.DefaultWorkerPath = workerDescription.GetWorkerPath();
                if (string.IsNullOrEmpty(workerDescription.DefaultWorkerPath))
                {
                    if (!Path.IsPathRooted(workerDescription.DefaultExecutablePath))
                    {
                        workerDescription.DefaultExecutablePath = Path.Combine(workerDescription.WorkerDirectory, workerDescription.DefaultExecutablePath);
                    }
                }
            }
        }
    }
}
