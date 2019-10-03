﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
        private ScriptJobHostOptions _scriptJobHostOptions;

        public HttpInvokerOptionsSetup(IOptions<ScriptJobHostOptions> scriptJobHostOptions, IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _scriptJobHostOptions = scriptJobHostOptions.Value;
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
                HttpInvokerDescription httpInvokerDescription = options.Description;

                if (httpInvokerDescription == null)
                {
                    throw new HostConfigurationException($"WorkerDescription for HttpInvoker is requuired");
                }
                httpInvokerDescription.FixAndValidate(_scriptJobHostOptions.RootScriptPath);
                options.Arguments = new WorkerProcessArguments()
                {
                    ExecutablePath = options.Description.DefaultExecutablePath,
                    WorkerPath = options.Description.DefaultWorkerPath
                };

                options.Arguments.ExecutableArguments.AddRange(options.Description.Arguments);
                _logger.LogDebug("Configured httpInvoker with {DefaultExecutablePath}: {exepath} with arguments {args}", nameof(options.Description.DefaultExecutablePath), options.Description.DefaultExecutablePath, options.Arguments);
            }
        }
    }
}
