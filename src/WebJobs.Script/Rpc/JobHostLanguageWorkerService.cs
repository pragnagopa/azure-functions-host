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
    public class JobHostLanguageWorkerService : IJobHostLanguageWorkerService
    {
        private readonly IScriptEventManager _eventManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly IOptionsMonitor<ScriptApplicationHostOptions> _applicationHostOptions;
        private IEnumerable<WorkerConfig> _workerConfigs;
        private Dictionary<string, ILanguageWorkerChannel> _jobHostChannels = new Dictionary<string, ILanguageWorkerChannel>();
        private IList<IDisposable> _eventSubscriptions = new List<IDisposable>();
        private IRpcServer _rpcServer;
        private IWorkerProcessFactory _processFactory;
        private IProcessRegistry _processRegistry;
        private string _currentLanguageRuntime;

        public JobHostLanguageWorkerService(IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions, IOptions<LanguageWorkerOptions> languageWorkerOptions, ILoggerFactory loggerFactory, IServiceProvider rootServiceProvider)
        {
            _eventManager = (IScriptEventManager)rootServiceProvider.GetService(typeof(IScriptEventManager));
            _rpcServer = (IRpcServer)rootServiceProvider.GetService(typeof(IRpcServer));
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger("Host.LanguageWorkerService.init");
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _applicationHostOptions = applicationHostOptions ?? throw new ArgumentNullException(nameof(applicationHostOptions));
            _processFactory = new DefaultWorkerProcessFactory();
            _processRegistry = ProcessRegistryFactory.Create();
            _currentLanguageRuntime = Environment.GetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeSettingName);
        }

        public IDictionary<string, ILanguageWorkerChannel> JobHostChannels => _jobHostChannels;

        public Task InitializeJobHostChannelAsync()
        {
            _logger?.LogInformation("in InitializeJobHostChannelAsync...");
            string scriptRootPath = _applicationHostOptions.CurrentValue.ScriptPath;
            WorkerConfig currentLanguageWorkerConfig = _workerConfigs.Where(c => c.Language.Equals(_currentLanguageRuntime, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            return InitializeLanguageWorkerChannelAsync(currentLanguageWorkerConfig, scriptRootPath, false);
        }

        private Task InitializeLanguageWorkerChannelAsync(WorkerConfig workerConfig, string scriptRootPath, bool isPlaceHolderChannel)
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
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            foreach (ILanguageWorkerChannel languageWorkerChannel in _jobHostChannels.Values)
            {
                languageWorkerChannel.Dispose();
            }
        }
    }
}
