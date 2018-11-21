// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class JobHostChannelInitializationService : IHostedService
    {
        private IJobHostLanguageWorkerService _languageWorkerService;
        private string _currentLanguageRuntime;

        public JobHostChannelInitializationService(IJobHostLanguageWorkerService languageWorkerService)
        {
            _languageWorkerService = languageWorkerService;
            _currentLanguageRuntime = Environment.GetEnvironmentVariable(LanguageWorkerConstants.FunctionWorkerRuntimeSettingName);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
            // TODO this StartAsync needs to complete before funtion descriptor
            //return _languageWorkerService.InitializeJobHostChannelAsync(_currentLanguageRuntime);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // TODO Dispose language workers
            _languageWorkerService.Dispose();
            return Task.CompletedTask;
        }
    }
}
