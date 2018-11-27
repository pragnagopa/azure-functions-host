// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class LanguageWorkerChannelManager : ILanguageWorkerChannelManager
    {
        private readonly IEnumerable<WorkerConfig> _workerConfigs = null;
        private readonly ILogger _logger = null;
        private readonly IOptionsMonitor<ScriptApplicationHostOptions> _applicationHostOptions = null;
        private IScriptEventManager _eventManager = null;
        private ILoggerFactory _loggerFactory = null;
        private IWorkerProcessFactory _processFactory;
        private IProcessRegistry _processRegistry;
        private IRpcServer _rpcServer = null;
        private IMetricsLogger _metricsLogger = null;
        private IList<IDisposable> _eventSubscriptions = new List<IDisposable>();
        private IDictionary<string, ILanguageWorkerChannel> _webhostChannels = new Dictionary<string, ILanguageWorkerChannel>();

        public LanguageWorkerChannelManager(IScriptEventManager eventManager, IRpcServer rpcServer, ILoggerFactory loggerFactory, IOptions<LanguageWorkerOptions> languageWorkerOptions, IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions)
        {
            _rpcServer = rpcServer;
            _eventManager = eventManager;
            //_metricsLogger = metricsLogger;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger("Host.FunctionDispatcher");
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _applicationHostOptions = applicationHostOptions;
            _processFactory = new DefaultWorkerProcessFactory();
            try
            {
                _processRegistry = ProcessRegistryFactory.Create();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to create process registry");
            }
        }

        public IDictionary<string, ILanguageWorkerChannel> WebhostChannels { get => _webhostChannels; set => _webhostChannels = value; }

        public ILanguageWorkerChannel CreateLanguageWorkerChannel(string scriptRootPath, string language, bool isJobHostChannel, int attemptCount)
        {
            return CreateLanguageWorkerChannel(scriptRootPath, language, null, isJobHostChannel, attemptCount);
        }

        public ILanguageWorkerChannel CreateLanguageWorkerChannel(string scriptRootPath, string language, IObservable<FunctionRegistrationContext> functionRegistrations, bool isJobHostChannel, int attemptCount)
        {
            var languageWorkerConfig = _workerConfigs.Where(c => c.Language.Equals(language, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (languageWorkerConfig == null)
            {
                throw new InvalidOperationException($"WorkerCofig for language: {language} not found");
            }
            return new LanguageWorkerChannel(
                         scriptRootPath,
                         _eventManager,
                         functionRegistrations,
                         _processFactory,
                         _processRegistry,
                         languageWorkerConfig,
                         _rpcServer.Uri,
                         _loggerFactory,
                         _metricsLogger,
                         isJobHostChannel,
                         attemptCount);
        }

        public void InitializeMetricsLogger(IMetricsLogger metricsLogger)
        {
            _metricsLogger = metricsLogger;
        }

        public Task InitializeWebhostChannelsAsync(IEnumerable<string> languages)
        {
            _logger?.LogInformation("in InitializeLanguageWorkerProcess...");
            foreach (string lang in languages)
            {
                InitializeLanguageWorkerChannel(lang, _applicationHostOptions.CurrentValue.ScriptPath, false);
            }
            return Task.CompletedTask;
        }

        private void InitializeLanguageWorkerChannel(string language, string scriptRootPath, bool isJobHostChannel)
        {
            try
            {
                string workerId = Guid.NewGuid().ToString();
                _logger.LogInformation("Creating languageChannelWorker...");
                // TODO attempt count
                ILanguageWorkerChannel languageWorkerChannel = CreateLanguageWorkerChannel(scriptRootPath, language, isJobHostChannel, 0);
                _logger.LogInformation($"_languageWorkerChannel null...{languageWorkerChannel == null}");
                languageWorkerChannel.CreateWorkerContextAndStartProcess();
                _eventSubscriptions.Add(_eventManager.OfType<RpcChannelReadyEvent>()
                .Subscribe(evt =>
                {
                    _webhostChannels.Add(evt.Language, evt.LanguageWorkerChannel);
                }));
            }
            catch (Exception grpcInitEx)
            {
                throw new HostInitializationException($"Failed to start Grpc Service. Check if your app is hitting connection limits.", grpcInitEx);
            }
        }
    }
}
