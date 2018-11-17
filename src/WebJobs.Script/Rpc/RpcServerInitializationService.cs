// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class RpcServerInitializationService : IHostedService
    {
        private IRpcServer _rpcServer;

        public RpcServerInitializationService(IRpcServer rpcServer)
        {
            _rpcServer = rpcServer ?? throw new ArgumentNullException(nameof(rpcServer));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
               return _rpcServer.StartAsync();
            }
            catch (Exception grpcInitEx)
            {
                var hostInitEx = new HostInitializationException($"Failed to start Grpc Service. Check if your app is hitting connection limits.", grpcInitEx);
                return Task.FromException(hostInitEx);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _rpcServer.ShutdownAsync();
        }
    }
}
