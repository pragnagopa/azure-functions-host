// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class PlaceHolderChannelsInitializationService : IHostedService
    {
        private IPlaceHolderLanguageWorkerService _languageWorkerService;

        public PlaceHolderChannelsInitializationService(IPlaceHolderLanguageWorkerService languageWorkerService)
        {
            _languageWorkerService = languageWorkerService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _languageWorkerService.InitializePlaceHolderChannelsAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //TODO Dispose language workers
            //_languageWorkerService.Dispose();
            return Task.CompletedTask;
        }
    }
}
