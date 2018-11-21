// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.Eventing.Rpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class LanguageWorkerService : ILanguageWorkerService
    {
        private readonly IScriptEventManager _eventManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly IOptionsMonitor<ScriptApplicationHostOptions> _applicationHostOptions;
        private IEnumerable<WorkerConfig> _workerConfigs;
        private Dictionary<string, ILanguageWorkerChannel> _languageWorkerChannels = new Dictionary<string, ILanguageWorkerChannel>();
        private IList<IDisposable> _eventSubscriptions = new List<IDisposable>();

        public LanguageWorkerService(IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions, IOptions<LanguageWorkerOptions> languageWorkerOptions, ILoggerFactory loggerFactory, IServiceProvider rootServiceProvider)
        {
            _eventManager = (IScriptEventManager)rootServiceProvider.GetService(typeof(IScriptEventManager));
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger("Host.LanguageWorkerService.init");
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _applicationHostOptions = applicationHostOptions ?? throw new ArgumentNullException(nameof(applicationHostOptions));
        }

        public IDictionary<string, ILanguageWorkerChannel> LanguageWorkerChannels => _languageWorkerChannels;

        public Task InitializeLanguageWorkerChannelsAsync(IRpcServer rpcServer)
        {
            _logger?.LogInformation("in InitializeLanguageWorkerProcess...");
            string scriptRootPath = _applicationHostOptions.CurrentValue.ScriptPath;
            var processFactory = new DefaultWorkerProcessFactory();
            IProcessRegistry processRegistry = ProcessRegistryFactory.Create();
            foreach (WorkerConfig workerConfig in _workerConfigs)
            {
                InitializeLanguageWorkerChannel(processFactory, processRegistry, workerConfig, scriptRootPath, rpcServer);
            }
            return Task.CompletedTask;
        }

        private void InitializeLanguageWorkerChannel(IWorkerProcessFactory processFactory, IProcessRegistry processRegistry, WorkerConfig workerConfig, string scriptRootPath, IRpcServer rpcServer)
        {
            try
            {
                string workerId = Guid.NewGuid().ToString();
                _logger.LogInformation("Creating languageChannelWorker...");
                ILanguageWorkerChannel languageWorkerChannel = new LanguageWorkerChannel(_eventManager, _logger, processFactory, processRegistry, workerConfig, workerId, rpcServer);
                _logger.LogInformation($"_languageWorkerChannel null...{languageWorkerChannel == null}");
                languageWorkerChannel.StartWorkerProcess(scriptRootPath);
                languageWorkerChannel.InitializeWorker();
                languageWorkerChannel.RpcServer = rpcServer;
                _eventSubscriptions.Add(_eventManager.OfType<RpcChannelReadyEvent>()
                .Subscribe(evt =>
                {
                    _languageWorkerChannels.Add(evt.Language, evt.LanguageWorkerChannel);
                }));
            }
            catch (Exception grpcInitEx)
            {
                throw new HostInitializationException($"Failed to start Grpc Service. Check if your app is hitting connection limits.", grpcInitEx);
            }
        }

        public void Dispose()
        {
            foreach (ILanguageWorkerChannel languageWorkerChannel in _languageWorkerChannels.Values)
            {
                languageWorkerChannel.Dispose();
            }
        }
    }
}
