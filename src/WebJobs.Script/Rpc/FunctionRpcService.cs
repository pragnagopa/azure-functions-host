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

        public FunctionRpcService(IScriptEventManager eventManager, ILoggerFactory loggerFactory)
        {
            _eventManager = eventManager;
            _logger = loggerFactory.CreateLogger(ScriptConstants.LogCategoryFunctionRpcService);
        }

        public void BufferInvoctionRequest(StreamingMessage message)
        {
            _outputMessageBag.Add(message);
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
                    outboundEventSubscription = _eventManager.OfType<OutboundEvent>()
                       .Where(evt => evt.WorkerId == workerId)
                       .ObserveOn(NewThreadScheduler.Default)
                       .Subscribe(evt =>
                       {
                           try
                           {
                               _outputMessageBag.Add(evt.Message);
                           }
                           catch (Exception subscribeEventEx)
                           {
                               _logger.LogError(subscribeEventEx, "Error reading message from Rpc channel");
                           }
                       });

                    _logger.LogInformation($"Received start stream..workerId: {workerId}");
                    InboundEvent startStreamEvent = new InboundEvent(workerId, requestStream.Current);
                    startStreamEvent.RpcRequestStream = requestStream;
                    _eventManager.Publish(startStreamEvent);
                    do
                    {
                        if (_outputMessageBag.TryTake(out StreamingMessage writeMessage))
                        {
                            await responseStream.WriteAsync(writeMessage);
                        }
                    }
                    while (true);
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
