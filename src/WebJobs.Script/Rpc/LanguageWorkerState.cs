// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Microsoft.Azure.WebJobs.Script.Description;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class LanguageWorkerState
    {
        private object _lock = new object();

        private IList<ILanguageWorkerChannel> _channels = new List<ILanguageWorkerChannel>();

        internal ILanguageWorkerChannel Channel { get; set; }

        internal List<Exception> Errors { get; set; } = new List<Exception>();

        // Registered list of functions which can be replayed if the worker fails to start / errors
        internal ReplaySubject<FunctionMetadata> Functions { get; set; } = new ReplaySubject<FunctionMetadata>();

        internal void AddChannel(ILanguageWorkerChannel channel)
        {
            lock (_lock)
            {
                _channels.Add(channel);
            }
        }

        internal IEnumerable<ILanguageWorkerChannel> GetChannels()
        {
            lock (_lock)
            {
                return _channels.ToList();
            }
        }
    }
}
