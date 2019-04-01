// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    internal class FunctionDispatcherLoadBalancer : IFunctionDispatcherLoadBalancer
    {
        private int _maxProcessCount = 1;
        private static int _counter = 1;
        private static object _functionLoadResponseLock = new object();

        internal FunctionDispatcherLoadBalancer(int maxProcessCount)
        {
            _maxProcessCount = maxProcessCount > _maxProcessCount ? maxProcessCount : _maxProcessCount;
        }

        public ILanguageWorkerChannel GetLanguageWorkerChannel(IEnumerable<ILanguageWorkerChannel> languageWorkers)
        {
            var currentNumberOfWorkers = languageWorkers.Count();
            if (currentNumberOfWorkers == 0)
            {
                throw new ArgumentOutOfRangeException($"Number of language workers:{currentNumberOfWorkers}");
            }
            ILanguageWorkerChannel lw = languageWorkers.ElementAt(_counter % currentNumberOfWorkers);
            lock (_functionLoadResponseLock)
            {
                if (_counter < _maxProcessCount)
                {
                    _counter++;
                }
                else
                {
                    _counter = 1;
                }
            }
            return lw;
        }
    }
}
