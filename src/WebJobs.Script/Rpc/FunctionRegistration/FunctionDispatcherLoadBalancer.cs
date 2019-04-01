// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class FunctionDispatcherLoadBalancer : IFunctionDispatcherLoadBalancer
    {
        private int _maxProcessCount = 1;
        private static int _counter = 1;

        internal FunctionDispatcherLoadBalancer(int maxProcessCount)
        {
            _maxProcessCount = maxProcessCount > _maxProcessCount ? maxProcessCount : _maxProcessCount;
        }

        public ILanguageWorkerChannel GetLanguageWorkerChannel(IEnumerable<ILanguageWorkerChannel> languageWorkerChannels)
        {
            if (languageWorkerChannels == null)
            {
                throw new ArgumentNullException(nameof(languageWorkerChannels));
            }

            var currentNumberOfWorkers = languageWorkerChannels.Count();

            if (currentNumberOfWorkers == 0)
            {
                throw new InvalidOperationException($"Number of language workers:{currentNumberOfWorkers}");
            }
            if (currentNumberOfWorkers > _maxProcessCount)
            {
                throw new InvalidOperationException($"Number of language workers exceeded:{currentNumberOfWorkers} exceeded maxProcessCount: {_maxProcessCount}");
            }
            ILanguageWorkerChannel workerChannel = languageWorkerChannels.ElementAt(_counter % currentNumberOfWorkers);
            if (_counter < currentNumberOfWorkers)
            {
                Interlocked.Increment(ref _counter);
            }
            else
            {
                _counter = 1;
            }
            return workerChannel;
        }
    }
}
