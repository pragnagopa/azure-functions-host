// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class ProcessPoolLoadBalancer : IFunctionDispatcherLoadBalancer
    {
        private int _maxProcessCount = 1;
        private int _processPoolCount = 1;
        private int _functionsCount = 1;
        private static int _counter = 1;
        private static object _functionLoadResponseLock = new object();
        private IDictionary<string, int> _functionAffinityPools = new Dictionary<string, int>();

        // PoolID, WokerId list
        private IDictionary<int, List<ILanguageWorkerChannel>> _workerChannelPools;

        // PoolId, FunctionId list
        private IDictionary<int, List<string>> _functionPools;

        internal ProcessPoolLoadBalancer(int maxProcessCount, int processPoolCount, int functionsCount)
        {
            _maxProcessCount = maxProcessCount > _maxProcessCount ? maxProcessCount : _maxProcessCount;
            _processPoolCount = processPoolCount > _processPoolCount ? processPoolCount : _processPoolCount;
            _functionsCount = functionsCount;
            _functionPools = new Dictionary<int, List<string>>();
            for (int poolIndex = 0; poolIndex < functionsCount; poolIndex++)
            {
                _functionPools[poolIndex] = new List<string>();
            }
        }

        public ILanguageWorkerChannel GetLanguageWorkerChannel(IEnumerable<ILanguageWorkerChannel> languageWorkers, string functionId)
        {
            if (functionId == null)
            {
                throw new ArgumentNullException(nameof(functionId));
            }
            if (_functionAffinityPools.TryGetValue(functionId, out int poolId))
            {
                return GetChannelForFunctionId(functionId, poolId);
            }
            // TOO improve
            PopulateWorkerPools(languageWorkers);

            // Pick a pool with least number of functions
            var orderedPools = _functionPools.OrderBy(fp => fp.Value.Count());
            orderedPools.ElementAt(0).Value.Add(functionId);
            // TODO remove _functionAffinityPools
            _functionAffinityPools.Add(functionId, orderedPools.ElementAt(0).Key);

            return GetChannelForFunctionId(functionId, orderedPools.ElementAt(0).Key);
        }

        private ILanguageWorkerChannel GetChannelForFunctionId(string functionId, int poolId)
        {
            if (functionId == null)
            {
                throw new ArgumentNullException(nameof(functionId));
            }
            return GetLanguageWorkerChannelFromPool(_workerChannelPools[poolId]);
        }

        public ILanguageWorkerChannel GetLanguageWorkerChannelFromPool(IEnumerable<ILanguageWorkerChannel> languageWorkers)
        {
            var currentNumberOfWorkers = languageWorkers.Count();
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

        internal void PopulateWorkerPools(IEnumerable<ILanguageWorkerChannel> languageWorkers)
        {
            _workerChannelPools = new Dictionary<int, List<ILanguageWorkerChannel>>();
            for (int poolIndex = 0; poolIndex < _processPoolCount; poolIndex++)
            {
                _workerChannelPools[poolIndex] = new List<ILanguageWorkerChannel>();
            }
            for (int i = 0; i < languageWorkers.Count();)
            {
                for (int j = 0; j < _workerChannelPools.Count(); j++)
                {
                    if (languageWorkers.ElementAtOrDefault(i) == null)
                    {
                        break;
                    }
                    _workerChannelPools[j].Add(languageWorkers.ElementAt(i));
                    i++;
                }
            }
        }

        internal void AssingFunctioToWorkerPool(IEnumerable<ILanguageWorkerChannel> languageWorkers)
        {
            var orderedPools = _functionPools.OrderBy(fp => fp.Value.Count());
        }

        public ILanguageWorkerChannel GetLanguageWorkerChannel(IEnumerable<ILanguageWorkerChannel> languageWorkers)
        {
            throw new NotImplementedException();
        }
    }
}
