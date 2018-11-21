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
    public class PlaceHolderLanguageWorkerService : IPlaceHolderLanguageWorkerService
    {
        private readonly IScriptEventManager _eventManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly IOptionsMonitor<ScriptApplicationHostOptions> _applicationHostOptions;
        private IEnumerable<WorkerConfig> _workerConfigs;
        private Dictionary<string, ILanguageWorkerChannel> _placeholderChannels = new Dictionary<string, ILanguageWorkerChannel>();
        private Dictionary<string, ILanguageWorkerChannel> _jobHostChannels = new Dictionary<string, ILanguageWorkerChannel>();
        private IList<IDisposable> _eventSubscriptions = new List<IDisposable>();
        private IRpcServer _rpcServer;
        private IWorkerProcessFactory _processFactory;
        private IProcessRegistry _processRegistry;

        public PlaceHolderLanguageWorkerService(IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions, IOptions<LanguageWorkerOptions> languageWorkerOptions, ILoggerFactory loggerFactory, IServiceProvider rootServiceProvider)
        {
            _eventManager = (IScriptEventManager)rootServiceProvider.GetService(typeof(IScriptEventManager));
            _rpcServer = (IRpcServer)rootServiceProvider.GetService(typeof(IRpcServer));
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger("Host.LanguageWorkerService.init");
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _applicationHostOptions = applicationHostOptions ?? throw new ArgumentNullException(nameof(applicationHostOptions));
            _processFactory = new DefaultWorkerProcessFactory();
            _processRegistry = ProcessRegistryFactory.Create();
        }

        public IDictionary<string, ILanguageWorkerChannel> PlaceHolderChannels => _placeholderChannels;

        public IDictionary<string, ILanguageWorkerChannel> JobHostChannels => _jobHostChannels;

        public Task InitializePlaceHolderChannelsAsync()
        {
            _logger?.LogInformation("in InitializeLanguageWorkerProcess...");
            string scriptRootPath = _applicationHostOptions.CurrentValue.ScriptPath;
            WorkerConfig javaWorkerConfig = _workerConfigs.Where(c => c.Language.Equals(LanguageWorkerConstants.JavaLanguageWorkerName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            InitializeLanguageWorkerChannel(javaWorkerConfig, scriptRootPath, true);
            return Task.CompletedTask;
        }

        public Task InitializeJobHostChannelAsync(string currentLanguageRuntime)
        {
            _logger?.LogInformation("in InitializeJobHostChannelAsync...");
            string scriptRootPath = _applicationHostOptions.CurrentValue.ScriptPath;
            WorkerConfig currentLanguageWorkerConfig = _workerConfigs.Where(c => c.Language.Equals(currentLanguageRuntime, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            InitializeLanguageWorkerChannel(currentLanguageWorkerConfig, scriptRootPath, false);
            return Task.CompletedTask;
        }

        private void InitializeLanguageWorkerChannel(WorkerConfig workerConfig, string scriptRootPath, bool isPlaceHolderChannel)
        {
            try
            {
                string workerId = Guid.NewGuid().ToString();
                _logger.LogInformation("Creating languageChannelWorker...");
                ILanguageWorkerChannel languageWorkerChannel = new LanguageWorkerChannel(_eventManager, _logger, _processFactory, _processRegistry, workerConfig, workerId, _rpcServer, isPlaceHolderChannel);
                _logger.LogInformation($"_languageWorkerChannel null...{languageWorkerChannel == null}");
                languageWorkerChannel.StartWorkerProcess(scriptRootPath);
                languageWorkerChannel.InitializeWorker();
                languageWorkerChannel.RpcServer = _rpcServer;
                _eventSubscriptions.Add(_eventManager.OfType<RpcChannelReadyEvent>()
                .Subscribe(evt =>
                {
                    if (evt.IsPlaceHolderChannel)
                    {
                        _placeholderChannels.Add(evt.Language, evt.LanguageWorkerChannel);
                    }
                    else
                    {
                        _jobHostChannels.Add(evt.Language, evt.LanguageWorkerChannel);
                    }
                }));
            }
            catch (Exception grpcInitEx)
            {
                throw new HostInitializationException($"Failed to start Grpc Service. Check if your app is hitting connection limits.", grpcInitEx);
            }
        }

        public void Dispose()
        {
            // TODO
            //foreach (ILanguageWorkerChannel languageWorkerChannel in _placeholderChannels.Values)
            //{
            //    languageWorkerChannel.Dispose();
            //}
        }
    }
}
