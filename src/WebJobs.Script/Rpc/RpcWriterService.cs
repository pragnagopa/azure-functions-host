// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class RpcWriterService : BackgroundService
    {
        private readonly IOptionsMonitor<ScriptApplicationHostOptions> _applicationHostOptions;
        private readonly IEnvironment _environment;
        private readonly ILanguageWorkerChannelManager _languageWorkerChannelManager;
        private readonly IRpcServer _rpcServer;
        private readonly ILogger _logger;
        private FunctionRpcService _functionRpcService;

        private List<string> _languages = new List<string>()
        {
            LanguageWorkerConstants.JavaLanguageWorkerName
        };

        public RpcWriterService(IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions, IEnvironment environment, IRpcServer rpcServer, ILanguageWorkerChannelManager languageWorkerChannelManager, ILoggerFactory loggerFactory, FunctionRpc.FunctionRpcBase functionRpcBase)
        {
            _applicationHostOptions = applicationHostOptions ?? throw new ArgumentNullException(nameof(applicationHostOptions));
            _logger = loggerFactory.CreateLogger(ScriptConstants.LogCategoryRpcInitializationService);
            _rpcServer = rpcServer;
            _environment = environment;
            _functionRpcService = functionRpcBase as FunctionRpcService;
            _languageWorkerChannelManager = languageWorkerChannelManager ?? throw new ArgumentNullException(nameof(languageWorkerChannelManager));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() => _logger.LogDebug("Demo Service is stopping."));

            _logger.LogInformation("Starting Rpc Initialization Service.");
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var rpcWriteMsg in _functionRpcService.BlockingCollectionQueue.GetConsumingEnumerable())
                {
                    await _functionRpcService.ResponseStream.WriteAsync(rpcWriteMsg);
                }
            }
        }
    }
}
