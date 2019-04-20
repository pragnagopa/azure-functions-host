﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.Eventing.Rpc;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    // Implementation for the grpc service
    // TODO: move to WebJobs.Script.Grpc package and provide event stream abstraction
    internal class FunctionRpcService : FunctionRpc.FunctionRpcBase
    {
        private readonly IScriptEventManager _eventManager;
        private readonly ILogger _logger;
        private BlockingCollection<StreamingMessage> _blockingCollectionQueue = new BlockingCollection<StreamingMessage>();
        private Task _readerTask;

        public FunctionRpcService(IScriptEventManager eventManager, ILoggerFactory loggerFactory)
        {
            _eventManager = eventManager;
            _logger = loggerFactory.CreateLogger(ScriptConstants.LogCategoryFunctionRpcService);
        }

        public void AddWrite(StreamingMessage streamingMessage)
        {
            _blockingCollectionQueue.Add(streamingMessage);
        }

        public Task StartPolling()
        {
            return _readerTask;
        }

        public override async Task EventStream(IAsyncStreamReader<StreamingMessage> requestStream, IServerStreamWriter<StreamingMessage> responseStream, ServerCallContext context)
        {
            var cancelSource = new TaskCompletionSource<bool>();
            IDisposable outboundEventSubscription = null;
            try
            {
                context.CancellationToken.Register(() => cancelSource.TrySetResult(false));

                Func<Task<bool>> messageAvailable = async () =>
                {
                    // GRPC does not accept cancellation tokens for individual reads, hence wrapper
                    var requestTask = requestStream.MoveNext(CancellationToken.None);
                    var completed = await Task.WhenAny(cancelSource.Task, requestTask);
                    return completed.Result;
                };

                if (await messageAvailable())
                {
                    string workerId = requestStream.Current.StartStream.WorkerId;

                    var responseReaderTask = Task.Run(async () =>
                    {
                        while (await messageAvailable())
                        {
                            _eventManager.Publish(new InboundEvent(workerId, requestStream.Current));
                        }
                    });

                    _readerTask = Task.Factory.StartNew(async () => await messageAvailable());
                    _eventManager.Publish(new InboundEvent(workerId, requestStream.Current));
                    var consumer = Task.Run(async () =>
                    {
                        foreach (var rpcWriteMsg in _blockingCollectionQueue.GetConsumingEnumerable())
                        {
                            await responseStream.WriteAsync(rpcWriteMsg);
                        }
                    });
                    await consumer;
                }
            }
            finally
            {
                outboundEventSubscription?.Dispose();

                // ensure cancellationSource task completes
                cancelSource.TrySetResult(false);
            }
        }
    }
}
