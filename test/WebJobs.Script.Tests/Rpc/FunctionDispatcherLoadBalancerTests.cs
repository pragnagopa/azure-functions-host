// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.Rpc
{
    public class FunctionDispatcherLoadBalancerTests
    {
        [Fact]
        public static void LoadBalancer_EqualDistribution_EvenNumberOfChannels()
        {
            List<ILanguageWorkerChannel> results = new List<ILanguageWorkerChannel>();
            int totalInvocations = 100;

            IEnumerable<ILanguageWorkerChannel> workerChannels = new List<ILanguageWorkerChannel>()
            {
                new TestLanguageWorkerChannel("1"),
                new TestLanguageWorkerChannel("2"),
                new TestLanguageWorkerChannel("3"),
                new TestLanguageWorkerChannel("4")
            };

            int expectedInvocationsPerChannel = totalInvocations / workerChannels.Count();

            IFunctionDispatcherLoadBalancer loadBalancer = new FunctionDispatcherLoadBalancer(workerChannels.Count());
            for (int index = 0; index < 100; index++)
            {
                results.Add(loadBalancer.GetLanguageWorkerChannel(workerChannels));
            }
            var channelGroupsQuery = results.GroupBy(r => r.Id)
            .Select(g => new { Value = g.Key, Count = g.Count() });

            foreach (var channelGroup in channelGroupsQuery)
            {
                Assert.True(channelGroup.Count == expectedInvocationsPerChannel);
            }
        }

        [Fact]
        public static void LoadBalancer_OddNumberOfChannels()
        {
            List<ILanguageWorkerChannel> results = new List<ILanguageWorkerChannel>();
            int totalInvocations = 100;

            IEnumerable<ILanguageWorkerChannel> workerChannels = new List<ILanguageWorkerChannel>()
            {
                new TestLanguageWorkerChannel("1"),
                new TestLanguageWorkerChannel("2"),
                new TestLanguageWorkerChannel("3")
            };

            int expectedInvocationsPerChannel = totalInvocations / workerChannels.Count();

            IFunctionDispatcherLoadBalancer loadBalancer = new FunctionDispatcherLoadBalancer(workerChannels.Count());
            for (int index = 0; index < 100; index++)
            {
                results.Add(loadBalancer.GetLanguageWorkerChannel(workerChannels));
            }
            var channelGroupsQuery = results.GroupBy(r => r.Id)
            .Select(g => new { Value = g.Key, Count = g.Count() });

            foreach (var channelGroup in channelGroupsQuery)
            {
                Assert.True(channelGroup.Count >= expectedInvocationsPerChannel);
            }
        }

        [Fact]
        public static void LoadBalancer_Throws_InvalidOperationException_NoWorkerChannels()
        {
            List<ILanguageWorkerChannel> results = new List<ILanguageWorkerChannel>();
            IEnumerable<ILanguageWorkerChannel> workerChannels = new List<ILanguageWorkerChannel>();
            IFunctionDispatcherLoadBalancer loadBalancer = new FunctionDispatcherLoadBalancer(workerChannels.Count());

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                loadBalancer.GetLanguageWorkerChannel(workerChannels);
            });

            Assert.Equal($"Number of language workers:0", ex.Message);
        }

        [Fact]
        public static void LoadBalancer_Throws_InvalidOperationException_WorkerChannelsExceededProcessCount()
        {
            List<ILanguageWorkerChannel> results = new List<ILanguageWorkerChannel>();
            IEnumerable<ILanguageWorkerChannel> workerChannels = new List<ILanguageWorkerChannel>()
            {
                new TestLanguageWorkerChannel("1"),
                new TestLanguageWorkerChannel("2"),
                new TestLanguageWorkerChannel("3")
            };
            IFunctionDispatcherLoadBalancer loadBalancer = new FunctionDispatcherLoadBalancer(2);

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                loadBalancer.GetLanguageWorkerChannel(workerChannels);
            });

            Assert.Contains($"Number of language workers exceeded", ex.Message);
        }
    }
}
