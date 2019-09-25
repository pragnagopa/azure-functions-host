﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.WebJobs.Script.Abstractions;
using Microsoft.Azure.WebJobs.Script.OutOfProc;
using Microsoft.Azure.WebJobs.Script.Rpc;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Script.Tests.Rpc
{
    public class WorkerConfigTestUtilities
    {
        public const string TestWorkerPathInWorkerConfig = "./src/index";
        public const string HttpInvokerExe = "httpServer.exe";
        public const string TestDefaultExecutablePath = "testWorkerPath";

        public static JObject GetTestWorkerConfig(string language, string[] arguments, bool invalid, string profileName, bool emptyWorkerPath = false)
        {
            WorkerDescription description = GetTestDefaultWorkerDescription(language, arguments);

            JObject config = new JObject();
            config[OutOfProcConstants.WorkerDescription] = JObject.FromObject(description);

            if (!string.IsNullOrEmpty(profileName))
            {
                var appSvcDescription = new WorkerDescription()
                {
                    DefaultExecutablePath = "myFooPath",
                };

                JObject profiles = new JObject();
                profiles[profileName] = JObject.FromObject(appSvcDescription);
                config[OutOfProcConstants.WorkerDescriptionProfiles] = profiles;
            }

            if (invalid)
            {
                config[OutOfProcConstants.WorkerDescription] = "invalidWorkerConfig";
            }

            if (emptyWorkerPath)
            {
                config[OutOfProcConstants.WorkerDescription][OutOfProcConstants.WorkerDescriptionDefaultWorkerPath] = null;
            }

            return config;
        }

        public static WorkerDescription GetTestDefaultWorkerDescription(string language, string[] arguments)
        {
            return new WorkerDescription()
            {
                DefaultExecutablePath = TestDefaultExecutablePath,
                DefaultWorkerPath = $"{TestWorkerPathInWorkerConfig}.{language}",
                Language = language,
                Extensions = new List<string> { $".{language}" },
                Arguments = arguments.ToList()
            };
        }

        public static WorkerDescription GetTestHttpInvokerDescription(string[] arguments, bool invalid = false)
        {
            if (invalid)
            {
                return new WorkerDescription();
            }
            WorkerDescription workerDescription = new WorkerDescription()
            {
                DefaultExecutablePath = HttpInvokerExe
            };
            if (arguments != null)
            {
                workerDescription.Arguments = arguments.ToList();
            }
            return workerDescription;
        }

        public static JObject GetTestHttpInvokerConfig(string[] arguments, bool invalid = false)
        {
            WorkerDescription description = GetTestHttpInvokerDescription(arguments, invalid);

            JObject config = new JObject();
            config[OutOfProcConstants.WorkerDescription] = JObject.FromObject(description);
            return config;
        }

        public static void CreateWorkerFolder(string testDir, TestLanguageWorkerConfig workerConfig, bool createTestWorker = true)
        {
            string workerPath = string.IsNullOrEmpty(workerConfig.Language) ? testDir : Path.Combine(testDir, workerConfig.Language);
            Directory.CreateDirectory(workerPath);
            File.WriteAllText(Path.Combine(workerPath, LanguageWorkerConstants.WorkerConfigFileName), workerConfig.Json);
            if (createTestWorker)
            {
                Directory.CreateDirectory(Path.Combine(workerPath, $"{TestWorkerPathInWorkerConfig}"));
                File.WriteAllText(Path.Combine(workerPath, $"{TestWorkerPathInWorkerConfig}.{workerConfig.Language}"), "test worker");
            }
        }
    }
}
