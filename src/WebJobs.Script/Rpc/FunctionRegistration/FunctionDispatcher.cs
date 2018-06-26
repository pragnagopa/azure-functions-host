// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Eventing;
using FunctionMetadata = Microsoft.Azure.WebJobs.Script.Description.FunctionMetadata;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class FunctionDispatcher : IFunctionDispatcher
    {
        private IScriptEventManager _eventManager;
        private IRpcServer _server;
        private CreateChannel _channelFactory;
        private List<WorkerConfig> _workerConfigs;
        private ConcurrentDictionary<WorkerConfig, LanguageWorkerState> _channelState = new ConcurrentDictionary<WorkerConfig, LanguageWorkerState>();
        private IDisposable _workerErrorSubscription;
        private List<ILanguageWorkerChannel> _erroredChannels = new List<ILanguageWorkerChannel>();
        private ConcurrentDictionary<string, ILanguageWorkerChannel> _channelsDictionary = new ConcurrentDictionary<string, ILanguageWorkerChannel>();
        private bool disposedValue = false;

        public FunctionDispatcher(
            IScriptEventManager manager,
            IRpcServer server,
            CreateChannel channelFactory,
            IEnumerable<WorkerConfig> workers)
        {
            _eventManager = manager;
            _server = server;
            _channelFactory = channelFactory;
            _workerConfigs = workers?.ToList() ?? new List<WorkerConfig>();
            _workerErrorSubscription = _eventManager.OfType<WorkerErrorEvent>()
                .Subscribe(WorkerError);
        }

        public IDictionary<WorkerConfig, LanguageWorkerState> LanguageWorkerChannelsState => _channelState;

        public bool IsSupported(FunctionMetadata functionMetadata)
        {
            return _workerConfigs.Any(config => config.Extension == Path.GetExtension(functionMetadata.ScriptFile));
        }

        internal LanguageWorkerState CreateWorkerState(WorkerConfig config)
        {
            var state = new LanguageWorkerState();
            state.Channel = _channelFactory(config, state.Functions);
            _channelsDictionary[state.Channel.Id] = state.Channel;
            return state;
        }

        public void Register(FunctionRegistrationContext context)
        {
            WorkerConfig workerConfig = _workerConfigs.First(config => config.Extension == Path.GetExtension(context.Metadata.ScriptFile));
            var state = _channelState.GetOrAdd(workerConfig, CreateWorkerState);
            state.Functions.OnNext(context);
        }

        public void WorkerError(WorkerErrorEvent workerError)
        {
            ILanguageWorkerChannel erroredChannel;
            if (_channelsDictionary.TryGetValue(workerError.WorkerId, out erroredChannel))
            {
                // TODO: move retry logic, possibly into worker channel decorator
                _channelState.AddOrUpdate(erroredChannel.Config,
                    CreateWorkerState,
                    (config, state) =>
                    {
                        _erroredChannels.Add(state.Channel);
                        state.Errors.Add(workerError.Exception);
                        if (state.Errors.Count < 3)
                        {
                            state.Channel = _channelFactory(config, state.Functions);
                            _channelsDictionary[state.Channel.Id] = state.Channel;
                        }
                        else
                        {
                            var exception = new LanguageWorkerChannelException(state, $"Failed to start language worker for: {config.Language}");
                            var errorBlock = new ActionBlock<ScriptInvocationContext>(ctx =>
                            {
                                ctx.ResultSource.TrySetException(exception);
                            });
                            state.Functions.Subscribe(reg =>
                            {
                                state.RegisteredFunctions.Add(reg);
                                reg.InputBuffer.LinkTo(errorBlock);
                            });
                            _eventManager.Publish(new WorkerProcessErrorEvent(config.Language, exception));
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
                    foreach (var pair in _channelState)
                    {
                        pair.Value.Channel.Dispose();
                        pair.Value.Functions.Dispose();
                    }
                    foreach (var channel in _erroredChannels)
                    {
                        channel.Dispose();
                    }
                    _server.ShutdownAsync().GetAwaiter().GetResult();
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
