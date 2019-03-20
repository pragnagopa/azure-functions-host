﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.Eventing.Rpc;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;

using MsgType = Microsoft.Azure.WebJobs.Script.Grpc.Messages.StreamingMessage.ContentOneofCase;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    // Implementation for the grpc service
    // TODO: move to WebJobs.Script.Grpc package and provide event stream abstraction
    internal class FunctionRpcService : FunctionRpc.FunctionRpcBase
    {
        private readonly IScriptEventManager _eventManager;
        private readonly ILogger _logger;
        private readonly EventLoopScheduler _eventLoopScheduler;

        public FunctionRpcService(IScriptEventManager eventManager, ILoggerFactory loggerFactory)
        {
            _eventManager = eventManager;
            _logger = loggerFactory.CreateLogger(ScriptConstants.LogCategoryFunctionRpcService);
            _eventLoopScheduler = new EventLoopScheduler();
        }

        public override async Task EventStream(IAsyncStreamReader<StreamingMessage> requestStream, IServerStreamWriter<StreamingMessage> responseStream, ServerCallContext context)
        {
            var cancelSource = new TaskCompletionSource<bool>();
            _logger.LogInformation($"ServerCallContext context.status: {context.Status} context.Peer: {context.Peer}");
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
                    EventLoopScheduler eventLoopScheduler = new EventLoopScheduler();
                    outboundEventSubscription = _eventManager.OfType<OutboundEvent>()
                        .Where(evt => evt.WorkerId == workerId)
                        .ObserveOn(eventLoopScheduler)
                        .Subscribe(async evt =>
                        {
                            try
                            {
                                _logger.LogInformation($"ServerCallContext inside context.status: {context.Status} context.Peer: {context.Peer} ThreadID: {Thread.CurrentThread.ManagedThreadId} WorkerID: {workerId}");
                                // WriteAsync only allows one pending write at a time
                                // For each responseStream subscription, observe as a blocking write, in series, on a new thread
                                // Alternatives - could wrap responseStream.WriteAsync with a SemaphoreSlim to control concurrent access
                                if (evt.MessageType == MsgType.InvocationRequest)
                                {
                                    InvocationRequest invocationRequest = evt.Message.InvocationRequest;
                                    _logger.LogInformation($"WriteAsync invocationId: {invocationRequest.InvocationId} on threadid: {Thread.CurrentThread.ManagedThreadId}");
                                }
                                await responseStream.WriteAsync(evt.Message);
                                if (evt.MessageType == MsgType.InvocationRequest)
                                {
                                    InvocationRequest invocationRequest = evt.Message.InvocationRequest;
                                    _logger.LogInformation($"done WriteAsync invocationId: {invocationRequest.InvocationId}");
                                }
                            }
                            catch (Exception subscribeEventEx)
                            {
                                _logger.LogError(subscribeEventEx, $"Error writing message to Rpc channel worker id: {workerId}");
                            }
                        });

                    do
                    {
                        _logger.LogInformation($"Reading ThreadID: {Thread.CurrentThread.ManagedThreadId}");
                        _eventManager.Publish(new InboundEvent(workerId, requestStream.Current));
                    }
                    while (await messageAvailable());
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
