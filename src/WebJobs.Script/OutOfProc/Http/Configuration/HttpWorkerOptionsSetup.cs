// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Net.Sockets;
using Microsoft.Azure.WebJobs.Script.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc.Http
{
    internal class HttpWorkerOptionsSetup : IConfigureOptions<HttpWorkerOptions>
    {
        private IConfiguration _configuration;
        private ILogger _logger;
        private ScriptJobHostOptions _scriptJobHostOptions;

        public HttpWorkerOptionsSetup(IOptions<ScriptJobHostOptions> scriptJobHostOptions, IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _scriptJobHostOptions = scriptJobHostOptions.Value;
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<HttpWorkerOptionsSetup>();
        }

        public void Configure(HttpWorkerOptions options)
        {
            IConfigurationSection jobHostSection = _configuration.GetSection(ConfigurationSectionNames.JobHost);
            var httpInvokerSection = jobHostSection.GetSection(ConfigurationSectionNames.HttpInvoker);
            if (httpInvokerSection.Exists())
            {
                httpInvokerSection.Bind(options);
                HttpWorkerDescription httpInvokerDescription = options.Description;

                if (httpInvokerDescription == null)
                {
                    throw new HostConfigurationException($"Missing WorkerDescription for HttpInvoker");
                }
                if (!string.IsNullOrEmpty(httpInvokerDescription.BaseUri))
                {
                    return;
                }
                httpInvokerDescription.ApplyDefaultsAndValidate(_scriptJobHostOptions.RootScriptPath);
                options.Arguments = new WorkerProcessArguments()
                {
                    ExecutablePath = options.Description.DefaultExecutablePath,
                    WorkerPath = options.Description.DefaultWorkerPath
                };

                options.Arguments.ExecutableArguments.AddRange(options.Description.Arguments);
                options.Port = GetUnusedTcpPort();
                _logger.LogDebug("Configured httpInvoker with {DefaultExecutablePath}: {exepath} with arguments {args}", nameof(options.Description.DefaultExecutablePath), options.Description.DefaultExecutablePath, options.Arguments);
            }
        }

        internal static int GetUnusedTcpPort()
        {
            // TODO verify in Azure
            TcpListener tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();
            return port;
        }
    }
}