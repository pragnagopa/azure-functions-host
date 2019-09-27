// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    public abstract class WorkerDescription
    {
        /// <summary>
        /// Gets or sets the default executable path.
        /// </summary>
        [JsonProperty(PropertyName = "defaultExecutablePath")]
        public string DefaultExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the default path to the worker (relative to the bin/workers/{language} directory)
        /// </summary>
        [JsonProperty(PropertyName = "defaultWorkerPath")]
        public string DefaultWorkerPath
        {
            get
            {
                return DefaultWorkerPath;
            }

            set
            {
                if (string.IsNullOrEmpty(value) || Path.IsPathRooted(value))
                {
                    DefaultWorkerPath = value;
                }
                else
                {
                    DefaultWorkerPath = Path.Combine(WorkerDirectory, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the default base directory for the worker
        /// </summary>
        [JsonProperty(PropertyName = "workerDirectory")]
        public string WorkerDirectory
        {
            get
            {
                return WorkerDirectory;
            }

            set
            {
                WorkerDirectory = value;
                if (string.IsNullOrEmpty(value))
                {
                    WorkerDirectory = Directory.GetCurrentDirectory();
                }
            }
        }

        /// <summary>
        /// Gets or sets the default path to the worker (relative to the bin/workers/{language} directory)
        /// </summary>
        [JsonProperty(PropertyName = "arguments")]
        public List<string> Arguments
        {
            get
            {
                return Arguments;
            }

            set
            {
                Arguments = value;
                if (value == null)
                {
                    Arguments = new List<string>();
                }
            }
        }

        public abstract void Validate();
    }
}