// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Description;
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
        private ConcurrentBag<StreamingMessage> _outputMessageBag = new ConcurrentBag<StreamingMessage>();
        // private BlockingCollection<StreamingMessage> _blockingCollectionQueue = new BlockingCollection<StreamingMessage>();
        private ConcurrentBag<StreamingMessage> _blockingCollectionQueue = new ConcurrentBag<StreamingMessage>();

        public FunctionRpcService(IScriptEventManager eventManager, ILoggerFactory loggerFactory)
        {
            _eventManager = eventManager;
            _logger = loggerFactory.CreateLogger(ScriptConstants.LogCategoryFunctionRpcService);
        }

        public void BufferInvoctionRequest(StreamingMessage message)
        {
            // _outputMessageBag.Add(message);
            // Task writeTask = _rpcResponseStream.WriteAsync(message);
            _blockingCollectionQueue.Add(message);
        }

        public override async Task EventStream(IAsyncStreamReader<StreamingMessage> requestStream, IServerStreamWriter<StreamingMessage> responseStream, ServerCallContext context)
        {
            var cancelSource = new TaskCompletionSource<bool>();
            try
            {
                context.CancellationToken.Register(() => cancelSource.TrySetResult(false));

                await requestStream.MoveNext(CancellationToken.None);

                string workerId = requestStream.Current.StartStream.WorkerId;

                _logger.LogInformation($"Received start stream..workerId: {workerId}");
                InboundEvent startStreamEvent = new InboundEvent(workerId, requestStream.Current);
                startStreamEvent.RpcRequestStream = requestStream;
                _eventManager.Publish(startStreamEvent);
                var consumer = Task.Run(async () =>
                {
                    while (_blockingCollectionQueue.TryTake(out StreamingMessage rpcWriteMessage))
                    {
                        await responseStream.WriteAsync(rpcWriteMessage);
                    }
                });
                await consumer;
            }
            finally
            {
                // ensure cancellationSource task completes
                cancelSource.TrySetResult(false);
            }
        }
    }
}
