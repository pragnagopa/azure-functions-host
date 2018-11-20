// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class LanguageWorkerInitializationService : IHostedService
    {
        private IRpcServer _rpcServer;
        private ILanguageWorkerService _languageWorkerService;

        public LanguageWorkerInitializationService(IRpcServer rpcServer, ILanguageWorkerService languageWorkerService)
        {
            _rpcServer = rpcServer;
            _languageWorkerService = languageWorkerService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _languageWorkerService.InitializeLanguageWorkerChannelsAsync(_rpcServer);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Dispose language workers
            _languageWorkerService.Dispose();
            return Task.CompletedTask;
        }
    }
}
