// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.using System;

using Microsoft.Azure.WebJobs.Script.Rpc;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Script.Tests
{
    internal class RpcTestHelpers
    {
        internal static LanguageWorkerChannel GetCurrentWorkerChannel(IDictionary<string, LanguageWorkerState> channelStates, string runtime)
        {
            var nodeWorkerChannels = channelStates.Where(w => w.Key.Equals(runtime));
            return (LanguageWorkerChannel)nodeWorkerChannels.FirstOrDefault().Value.Channel;
        }

        internal static ILanguageWorkerProcess GetCurrentWorkerProcess(IDictionary<string, LanguageWorkerState> channelStates, string runtime)
        {
            var nodeChannelStates = channelStates.Where(w => w.Key.Equals(runtime));
            return (ILanguageWorkerProcess)nodeChannelStates.FirstOrDefault().Value.WorkerProcess;
        }

        internal static bool FunctionErrorsAdded(string functionName, ScriptHostEndToEndTestFixture fixture)
        {
            ICollection<string> funcErrors = null;
            return fixture.JobHost.FunctionErrors.TryGetValue(functionName, out funcErrors);
        }

        internal static void KillProcess(int oldProcId)
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/C taskkill /pid {oldProcId} /f";
            process.StartInfo = startInfo;
            process.Start();
        }

        internal static async Task WaitForWorkerProcessRestart(IDictionary<string, LanguageWorkerState> channelStates, string runtime, string oldWorkerId, ScriptHostEndToEndTestFixture fixture, string functionName)
        {
            await TestHelpers.Await(() =>
            {
                return GetCurrentWorkerChannel(channelStates, runtime).WorkerId != oldWorkerId
                         || FunctionErrorsAdded(functionName, fixture);

            }, pollingInterval: 4 * 1000, timeout: 60 * 1000);
           
        }

        internal static async Task WaitForJobHostRestart(IDictionary<string, LanguageWorkerState> channelStates, string runtime, string oldWorkerId)
        {
            await TestHelpers.Await(() =>
            {
                return GetCurrentWorkerChannel(channelStates, runtime).WorkerId != oldWorkerId;

            }, pollingInterval: 4 * 1000, timeout: 60 * 1000);

        }
    }
}
