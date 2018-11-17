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
        private ILanguageWorkerChannelManager _languageWorkerChannelManager;
        private ConcurrentDictionary<string, LanguageWorkerState> _channelStates = new ConcurrentDictionary<string, LanguageWorkerState>();
        private IDisposable _workerErrorSubscription;
        private IList<IDisposable> _workerStateSubscriptions = new List<IDisposable>();
        private ConcurrentDictionary<string, ILanguageWorkerChannel> _channelsDictionary = new ConcurrentDictionary<string, ILanguageWorkerChannel>();
        private string _language;
        private ILogger _logger;
        private ILoggerFactory _loggerFactory;
        private ScriptJobHostOptions _scriptOptions;
        private bool disposedValue = false;

        public FunctionDispatcher(IOptions<ScriptJobHostOptions> scriptHostOptions, IScriptEventManager eventManager, ILoggerFactory loggerFactory, IOptions<LanguageWorkerOptions> languageWorkerOptions, ILanguageWorkerChannelManager languageWorkerChannelManager)
        {
            _scriptOptions = scriptHostOptions.Value;
            _languageWorkerChannelManager = languageWorkerChannelManager;
            _eventManager = eventManager;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger("Host.FunctionDispatcher");
            _workerConfigs = languageWorkerOptions.Value.WorkerConfigs;
            //TODO:
            _language = Environment.GetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeSettingName);
            _channelFactory = (languageWorkerConfig, registrations, attemptCount) =>
             {
                 return _languageWorkerChannelManager.CreateLanguageWorkerChannel(_scriptOptions.RootScriptPath, _language, true, 0);
             };
            _workerErrorSubscription = _eventManager.OfType<WorkerErrorEvent>()
               .Subscribe(WorkerError);
        }

        public IDictionary<string, LanguageWorkerState> LanguageWorkerChannelStates => _channelStates;

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

        public LanguageWorkerState CreateWorkerStateWithExistingChannel(string language, ILanguageWorkerChannel languageWorkerChannel)
        {
            WorkerConfig config = _workerConfigs.Where(c => c.Language.Equals(language, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            _logger?.LogInformation("In CreateWorkerState ");
            var state = new LanguageWorkerState();
            state.Channel = languageWorkerChannel;
            _logger?.LogInformation($"state.Channel {state.Channel.Id} ");
            state.Channel.SetFunctionRegistrations(state.Functions);
            state.Channel.RegisterFunctions();
            _channelsDictionary[state.Channel.Id] = state.Channel;
            _logger?.LogInformation($"Added  workerState workerId: {state.Channel.Id}");
            return state;
        }

        public LanguageWorkerState CreateWorkerState(string language)
        {
            // TODO: pgopa figure out attempt count
            var state = new LanguageWorkerState();
            WorkerConfig config = _workerConfigs.Where(c => c.Language.Equals(language, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            state.Channel = _channelFactory(config, state.Functions, 0);
            _channelsDictionary[state.Channel.Id] = state.Channel;
            _channelStates[_language] = state;
            return state;
        }

        public void Register(FunctionRegistrationContext context)
        {
            string language = context.Metadata.Language;
            _logger?.LogInformation($"Register language {language}");
            var state = _channelStates.GetOrAdd(language, CreateWorkerState);
            state.Functions.OnNext(context);
        }

        public void RegisterFunctions()
        {
            var state = _channelStates[_language];
            state.Channel.SetFunctionRegistrations(state.Functions);
            state.Channel.RegisterFunctions();
        }

        public void WorkerError(WorkerErrorEvent workerError)
        {
            ILanguageWorkerChannel erroredChannel;
            if (_channelsDictionary.TryGetValue(workerError.WorkerId, out erroredChannel))
            {
                string language = erroredChannel.Config.Language;
                if (erroredChannel.IsPlaceHolderChannel)
                {
                    ILanguageWorkerChannel phChannel = null;
                    if (_languageWorkerChannelManager.InitializedChannels.TryGetValue(language, out phChannel))
                    {
                        _languageWorkerChannelManager.InitializedChannels.Remove(erroredChannel.Config.Language);
                        phChannel.Dispose();
                    }
                }
                erroredChannel.Dispose();
                LanguageWorkerState state = null;
                if (_channelStates.TryGetValue(erroredChannel.Config.Language, out state))
                {
                    state.Errors.Add(workerError.Exception);
                    if (state.Errors.Count < 3)
                    {
                        WorkerConfig config = _workerConfigs.Where(c => c.Language.Equals(language, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                        state.Channel = _channelFactory(config, state.Functions, state.Errors.Count);
                        state.Channel.SetFunctionRegistrations(state.Functions);
                        state.Channel.RegisterFunctions();
                        _channelsDictionary[language] = state.Channel;
                    }
                    else
                    {
                        var exMessage = $"Failed to start language worker for: {language}";
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
                        _eventManager.Publish(new WorkerProcessErrorEvent(state.Channel.Id, language, languageWorkerChannelException));
                    }
                }
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
