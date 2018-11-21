// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FunctionMetadata = Microsoft.Azure.WebJobs.Script.Description.FunctionMetadata;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class FunctionDispatcher : IFunctionDispatcher
    {
        private IScriptEventManager _eventManager;
        private CreateChannel _channelFactory;
        private IEnumerable<WorkerConfig> _workerConfigs;
        private ConcurrentDictionary<WorkerConfig, LanguageWorkerState> _channelStates = new ConcurrentDictionary<WorkerConfig, LanguageWorkerState>();
        private ConcurrentDictionary<string, LanguageWorkerState> _workerChannelStates = new ConcurrentDictionary<string, LanguageWorkerState>();
        private IDisposable _workerErrorSubscription;
        private IList<IDisposable> _workerStateSubscriptions = new List<IDisposable>();
        private ConcurrentDictionary<string, ILanguageWorkerChannel> _channelsDictionary = new ConcurrentDictionary<string, ILanguageWorkerChannel>();
        private string _language;
        private ILogger _logger;
        private ILoggerFactory _loggerFactory;
        private ScriptJobHostOptions _scriptOptions;
        private IWorkerProcessFactory _processFactory;
        private IProcessRegistry _processRegistry;
        private bool disposedValue = false;
        private IRpcServer _rpcServer;
        private IMetricsLogger _metricsLogger;

        public FunctionDispatcher(IOptions<ScriptJobHostOptions> scriptHostOptions, IScriptEventManager eventManager, IRpcServer rpcServer, IMetricsLogger metricsLogger, ILoggerFactory loggerFactory, IOptions<LanguageWorkerOptions> languageWorkerOptions)
        {
            _scriptOptions = scriptHostOptions.Value;
            _rpcServer = rpcServer;
            _eventManager = eventManager;
            _metricsLogger = metricsLogger;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger("Host.FunctionDispatcher");
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            _language = Environment.GetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeSettingName);
            _processFactory = new DefaultWorkerProcessFactory();
            try
            {
                _processRegistry = ProcessRegistryFactory.Create();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to create process registry");
            }
            _workerErrorSubscription = _eventManager.OfType<WorkerErrorEvent>()
               .Subscribe(WorkerError);
        }

        public IDictionary<WorkerConfig, LanguageWorkerState> LanguageWorkerChannelStates => _channelStates;

        public bool IsSupported(FunctionMetadata functionMetadata)
        {
            if (string.IsNullOrEmpty(functionMetadata.Language))
            {
                return false;
            }
            if (string.IsNullOrEmpty(_language))
            {
                return true;
            }
            return functionMetadata.Language.Equals(_language, StringComparison.OrdinalIgnoreCase);
        }

        public LanguageWorkerState CreateWorkerStateWithExistingChannel(WorkerConfig config, ILanguageWorkerChannel languageWorkerChannel)
        {
            _logger?.LogInformation("In CreateWorkerState ");
            var state = new LanguageWorkerState();
            state.Channel = languageWorkerChannel;
            _logger?.LogInformation($"state.Channel {state.Channel.Id} ");
            state.Channel.RegisterFunctions(state.Functions);
            _channelsDictionary[state.Channel.Id] = state.Channel;
            _workerChannelStates[config.Language] = state;
            _logger?.LogInformation($"Added  workerState workerId: {state.Channel.Id}");
            return state;
        }

        public LanguageWorkerState CreateWorkerState(WorkerConfig config)
        {
            var state = new LanguageWorkerState();
            if (_channelFactory == null)
            {
                _channelFactory = (languageWorkerConfig, registrations, attemptCount) =>
                {
                    return new LanguageWorkerChannel(
                        _scriptOptions,
                        _eventManager,
                        _processFactory,
                        _processRegistry,
                        registrations,
                        languageWorkerConfig,
                        _rpcServer.Uri,
                        _loggerFactory,
                        _metricsLogger,
                        attemptCount);
                };
            }
            state.Channel = _channelFactory(config, state.Functions, 0);
            _channelsDictionary[state.Channel.Id] = state.Channel;
            return state;
        }

        public void Register(FunctionRegistrationContext context)
        {
            _logger?.LogInformation($"in Register for function:{context.Metadata.Name}");
            string language = context.Metadata.Language;
            _logger?.LogInformation($"Register language {language}");
            WorkerConfig workerConfig = _workerConfigs.Where(c => c.Language == language).FirstOrDefault();
            LanguageWorkerState workerState = null;
            if (_workerChannelStates.TryGetValue(workerConfig.Language, out workerState))
            {
                _logger?.LogInformation($"Register workerState workerId: {workerState.Channel.Id}");
            }
            else
            {
                workerState = _channelStates.GetOrAdd(workerConfig, CreateWorkerState);
                _workerChannelStates[language] = workerState;
            }
            _logger?.LogInformation($"Register state.Functions.OnNext:context.InputBuffer.Count: {context.InputBuffer.Count}");
            _logger?.LogInformation($"Register state.Functions.Count(): {workerState.Functions.Count()}");
            workerState.Functions.OnNext(context);
        }

        public void WorkerError(WorkerErrorEvent workerError)
        {
            ILanguageWorkerChannel erroredChannel;
            if (_channelsDictionary.TryGetValue(workerError.WorkerId, out erroredChannel))
            {
                // TODO: move retry logic, possibly into worker channel decorator
                _channelStates.AddOrUpdate(erroredChannel.Config,
                    CreateWorkerState,
                    (config, state) =>
                    {
                        erroredChannel.Dispose();
                        state.Errors.Add(workerError.Exception);
                        if (state.Errors.Count < 3)
                        {
                            state.Channel = _channelFactory(config, state.Functions, state.Errors.Count);
                            _channelsDictionary[state.Channel.Id] = state.Channel;
                        }
                        else
                        {
                            var exMessage = $"Failed to start language worker for: {config.Language}";
                            var languageWorkerChannelException = (state.Errors != null && state.Errors.Count > 0) ? new LanguageWorkerChannelException(exMessage, new AggregateException(state.Errors.ToList())) : new LanguageWorkerChannelException(exMessage);
                            var errorBlock = new ActionBlock<ScriptInvocationContext>(ctx =>
                            {
                                ctx.ResultSource.TrySetException(languageWorkerChannelException);
                            });
                            _workerStateSubscriptions.Add(state.Functions.Subscribe(reg =>
                            {
                                state.AddRegistration(reg);
                                reg.InputBuffer.LinkTo(errorBlock);
                            }));
                            _eventManager.Publish(new WorkerProcessErrorEvent(state.Channel.Id, config.Language, languageWorkerChannelException));
                        }
                        return state;
                    });
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _workerErrorSubscription.Dispose();
                    foreach (var subscription in _workerStateSubscriptions)
                    {
                        subscription.Dispose();
                    }
                    foreach (var pair in _channelStates)
                    {
                        // TODO #3296 - send WorkerTerminate message to shut down language worker process gracefully (instead of just a killing)
                        pair.Value.Channel.Dispose();
                        pair.Value.Functions.Dispose();
                    }
                    //_server.ShutdownAsync().ContinueWith(t => t.Exception.Handle(e => true), TaskContinuationOptions.OnlyOnFaulted);
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
