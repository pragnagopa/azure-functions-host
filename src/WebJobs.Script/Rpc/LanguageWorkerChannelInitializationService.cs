// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class LanguageWorkerChannelInitializationService : IHostedService
    {
        private readonly IEnumerable<string> _languages = new List<string>()
        {
            LanguageWorkerConstants.JavaLanguageWorkerName
        };

        private ILanguageWorkerChannelManager _languageWorkerChannelManager;

        public LanguageWorkerChannelInitializationService(ILanguageWorkerChannelManager languageWorkerChannelManager)
        {
            _languageWorkerChannelManager = languageWorkerChannelManager ?? throw new ArgumentNullException(nameof(languageWorkerChannelManager));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _languageWorkerChannelManager.InitializePlaceHolderChannelsAsync(_languages);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //TODO Dispose language workers
            //_languageWorkerService.Dispose();
            return Task.CompletedTask;
        }
    }
}
