// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.Eventing.Rpc;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using MsgType = Microsoft.Azure.WebJobs.Script.Grpc.Messages.StreamingMessage.ContentOneofCase;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class LanguageWorkerLoadBalancer : ILanguageWorkerLoadBalancer
    {
        private readonly IScriptEventManager _eventManager;
        // A buffer to hold all the different invocation requests
        private IDictionary<string, BufferBlock<ScriptInvocationContext>> _invocationBuffer = new Dictionary<string, BufferBlock<ScriptInvocationContext>>();
        private BufferBlock<ScriptInvocationContext> _functionInputBuffer;

        // Need some list of the language worker channels -> could all of the dispatcher/channel manager/state/load balancer all use the same thing

        // Need to subscribe to the events
        private List<IDisposable> _eventSubscriptions = new List<IDisposable>();

        private IObservable<LoadBalancerEvent> _loadBalancerEvents;

        private IObservable<FunctionRegistrationContext> _functionRegistrations;
        private List<IDisposable> _inputLinks = new List<IDisposable>();

        public LanguageWorkerLoadBalancer(IScriptEventManager eventManager, IObservable<FunctionRegistrationContext> functionRegistrations)
        {
            _eventManager = eventManager;

            _loadBalancerEvents = _eventManager.OfType<LoadBalancerEvent>();

            _functionRegistrations = functionRegistrations;

            _eventSubscriptions.Add(_functionRegistrations.Subscribe(SendFunctionLoadRequest));
            _eventSubscriptions.Add(_loadBalancerEvents.Subscribe((msg) => ProcessLoad(msg)));
            _eventSubscriptions.Add(_loadBalancerEvents.Where(msg => msg.MessageType == MsgType.FunctionLoadResponse)
                .Subscribe((msg) => LoadResponse(msg.Message.FunctionLoadResponse)));
        }

        private void ProcessLoad(LoadBalancerEvent msg)
        {
            string workerId = msg.WorkerId;
            StreamingMessage message = msg.Message;

            //now we need to decide what the response should be -> this depends on what is going on with the language workers
            _eventManager.Publish(new InboundEvent(workerId, message));
        }

        public void AddChannel()
        {
            throw new NotImplementedException();
        }

        public void ProcessRequest(InvocationResponse invokeResponse)
        {
            InvocationResponse i = invokeResponse;
        }

        public void LoadResponse(FunctionLoadResponse loadResponse)
        {
            var inputBuffer = _functionInputBuffer;
            // link the invocation inputs to the invoke call
            var invokeBlock = new ActionBlock<ScriptInvocationContext>(ctx => ProcessInvocation(ctx));
            var disposableLink = inputBuffer.LinkTo(invokeBlock);
            _inputLinks.Add(disposableLink);
        }

        private void ProcessInvocation(ScriptInvocationContext context)
        {
            ScriptInvocationContext c = context;

            // YAAAAY I HAVE INTERCEPTED IT :D
        }

        internal void SendFunctionLoadRequest(FunctionRegistrationContext context)
        {
            FunctionMetadata metadata = context.Metadata;

            // associate the invocation input buffer with the function
            _functionInputBuffer = context.InputBuffer;
        }
    }
}
