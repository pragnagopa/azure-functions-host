﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.ManagedDependencies;
using Microsoft.Azure.WebJobs.Script.Properties;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FunctionMetadata = Microsoft.Azure.WebJobs.Script.Description.FunctionMetadata;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class LanguageWorkerChannelManager : ILanguageWorkerChannelManager
    {
        private readonly IEnumerable<WorkerConfig> _workerConfigs = null;
        private readonly ILogger _logger = null;
        private readonly TimeSpan processStartTimeout = TimeSpan.FromSeconds(40);
        private readonly TimeSpan workerInitTimeout = TimeSpan.FromSeconds(30);
        private readonly IOptionsMonitor<ScriptApplicationHostOptions> _applicationHostOptions = null;
        private readonly ILanguageWorkerConsoleLogSource _consoleLogSource;
        private readonly IScriptEventManager _eventManager = null;
        private readonly IEnvironment _environment;
        private readonly ILoggerFactory _loggerFactory = null;
        private readonly IWorkerProcessFactory _processFactory;
        private readonly IProcessRegistry _processRegistry;
        private readonly IRpcServer _rpcServer = null;
        private readonly IDisposable _rpcChannelReadySubscriptions;
        private string _workerRuntime;
        private Action _shutdownStandbyWorkerChannels;
        private IDictionary<string, LanguageWorkerState> _workerStates = new Dictionary<string, LanguageWorkerState>();

        public LanguageWorkerChannelManager(IScriptEventManager eventManager, IEnvironment environment, IRpcServer rpcServer, ILoggerFactory loggerFactory, IOptions<LanguageWorkerOptions> languageWorkerOptions,
            IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions, ILanguageWorkerConsoleLogSource consoleLogSource)
        {
            _rpcServer = rpcServer;
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _eventManager = eventManager;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<LanguageWorkerChannelManager>();
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _applicationHostOptions = applicationHostOptions;
            _consoleLogSource = consoleLogSource;

            _processFactory = new DefaultWorkerProcessFactory();
            try
            {
                _processRegistry = ProcessRegistryFactory.Create();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to create process registry");
            }

            _shutdownStandbyWorkerChannels = ScheduleShutdownStandbyChannels;
            _shutdownStandbyWorkerChannels = _shutdownStandbyWorkerChannels.Debounce(5000);
            _rpcChannelReadySubscriptions = _eventManager.OfType<RpcWebHostChannelReadyEvent>()
               .Subscribe(AddOrUpdateWorkerChannels);
        }

        public ILanguageWorkerChannel CreateLanguageWorkerChannel(string workerId, string scriptRootPath, string language, IMetricsLogger metricsLogger, int attemptCount, bool isWebhostChannel = false, IOptions<ManagedDependencyOptions> managedDependencyOptions = null)
        {
            var languageWorkerConfig = _workerConfigs.Where(c => c.Language.Equals(language, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (languageWorkerConfig == null)
            {
                throw new InvalidOperationException($"WorkerCofig for runtime: {language} not found");
            }
            return new LanguageWorkerChannel(
                         workerId,
                         scriptRootPath,
                         _eventManager,
                         _processFactory,
                         _processRegistry,
                         languageWorkerConfig,
                         _rpcServer.Uri,
                         _loggerFactory,
                         metricsLogger,
                         attemptCount,
                         _consoleLogSource,
                         isWebhostChannel,
                         managedDependencyOptions);
        }

        public Task<ILanguageWorkerChannel> InitializeChannelAsync(string runtime)
        {
            _logger?.LogDebug("Initializing language worker channel for runtime:{runtime}", runtime);
            LanguageWorkerState workerState = null;
            if (!_workerStates.TryGetValue(runtime, out workerState))
            {
                workerState = new LanguageWorkerState();
                _workerStates.Add(runtime, workerState);
            }
            return InitializeLanguageWorkerChannel(runtime, _applicationHostOptions.CurrentValue.ScriptPath);
        }

        private async Task<ILanguageWorkerChannel> InitializeLanguageWorkerChannel(string runtime, string scriptRootPath)
        {
            ILanguageWorkerChannel languageWorkerChannel = null;
            string workerId = Guid.NewGuid().ToString();
            _logger.LogInformation("Creating language worker channel for runtime:{runtime}", runtime);
            try
            {
                languageWorkerChannel = CreateLanguageWorkerChannel(workerId, scriptRootPath, runtime, null, 0, true);
                languageWorkerChannel.StartWorkerProcess();
                IObservable<RpcWebHostChannelReadyEvent> rpcChannelReadyEvent = _eventManager.OfType<RpcWebHostChannelReadyEvent>()
                                                                        .Where(msg => msg.Language == runtime).Timeout(workerInitTimeout);
                // Wait for response from language worker process
                RpcWebHostChannelReadyEvent readyEvent = await rpcChannelReadyEvent.FirstAsync();
            }
            catch (Exception ex)
            {
                throw new HostInitializationException($"Failed to start Language Worker Channel for language :{runtime}", ex);
            }
            return languageWorkerChannel;
        }

        public ILanguageWorkerChannel GetChannel(string language)
        {
            if (!string.IsNullOrEmpty(language) && _workerStates.TryGetValue(language, out LanguageWorkerState languageWorkerState))
            {
                return languageWorkerState.GetChannels().FirstOrDefault();
            }
            return null;
        }

        public IEnumerable<ILanguageWorkerChannel> GetChannels(string language)
        {
            if (!string.IsNullOrEmpty(language) && _workerStates.TryGetValue(language, out LanguageWorkerState languageWorkerState))
            {
                return languageWorkerState.GetChannels();
            }
            return null;
        }

        public async Task SpecializeAsync()
        {
            _logger.LogInformation(Resources.LanguageWorkerChannelSpecializationTrace);
            _workerRuntime = _environment.GetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeSettingName);
            ILanguageWorkerChannel languageWorkerChannel = GetChannel(_workerRuntime);
            if (_workerRuntime != null && languageWorkerChannel != null)
            {
                _logger.LogInformation("Loading environment variables for runtime: {runtime}", _workerRuntime);
                IObservable<WorkerProcessReadyEvent> processReadyEvents = _eventManager.OfType<WorkerProcessReadyEvent>()
                .Where(msg => string.Equals(msg.Language, _workerRuntime, StringComparison.OrdinalIgnoreCase))
                .Timeout(workerInitTimeout);
                languageWorkerChannel.SendFunctionEnvironmentReloadRequest();

                // Wait for response from language worker process
                await processReadyEvents.FirstAsync();
            }
            _shutdownStandbyWorkerChannels();
        }

        public bool ShutdownChannelsIfExist(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                throw new ArgumentNullException(nameof(language));
            }
            if (_workerStates.TryGetValue(language, out LanguageWorkerState languageWorkerState))
            {
                foreach (var channel in languageWorkerState.GetChannels().ToList())
                {
                    languageWorkerState.DisposeAndRemoveChannel(channel);
                }
                return true;
            }
            return false;
        }

        internal void ScheduleShutdownStandbyChannels()
        {
            _workerRuntime = _workerRuntime ?? _environment.GetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeSettingName);
            if (!string.IsNullOrEmpty(_workerRuntime))
            {
                var standbyWorkerStates = _workerStates.Where(ch => ch.Key.ToLower() != _workerRuntime.ToLower()).ToList();
                foreach (var state in standbyWorkerStates)
                {
                    _logger.LogInformation("Disposing standby channel for runtime:{language}", state.Key);
                    var channels = state.Value.GetChannels();
                    foreach (var channel in channels.ToList())
                    {
                        state.Value.DisposeAndRemoveChannel(channel);
                    }
                }
            }
        }

        public void ShutdownStandbyChannels(IEnumerable<FunctionMetadata> functions)
        {
            if (_environment.GetEnvironmentVariable(EnvironmentSettingNames.AzureWebsitePlaceholderMode) == "1")
            {
                return;
            }
            _workerRuntime = _environment.GetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeSettingName) ?? Utility.GetWorkerRuntime(functions);
            _logger.LogInformation("WorkerRuntime: {workerRuntime}. Will shutdown other standby channels", _workerRuntime);
            if (string.IsNullOrEmpty(_workerRuntime))
            {
                ShutdownChannels();
                return;
            }
            else
            {
                ScheduleShutdownStandbyChannels();
            }
        }

        public void ShutdownChannels()
        {
            foreach (string runtime in _workerStates.Keys)
            {
                var workerChannels = _workerStates[runtime].GetChannels();
                _logger.LogInformation("Shutting down language worker channels for runtime:{runtime}", runtime);
                foreach (var channel in workerChannels.ToList())
                {
                    _workerStates[runtime].DisposeAndRemoveChannel(channel);
                }
            }
            (_processRegistry as IDisposable)?.Dispose();
        }

        private void AddOrUpdateWorkerChannels(RpcWebHostChannelReadyEvent rpcChannelReadyEvent)
        {
            _logger.LogInformation("Adding language worker channel for runtime: {language}.", rpcChannelReadyEvent.Language);

            if (_workerStates.TryGetValue(rpcChannelReadyEvent.Language, out LanguageWorkerState languageWorkerState))
            {
                rpcChannelReadyEvent.LanguageWorkerChannel.RegisterFunctions(languageWorkerState.Functions);
                languageWorkerState.AddChannel(rpcChannelReadyEvent.LanguageWorkerChannel);
            }
        }
    }
}
