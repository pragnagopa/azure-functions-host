// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.Azure.WebJobs.Script.OutOfProc;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    public class RpcWorkerDescription : WorkerDescription
    {
        private List<string> _extensions = new List<string>();

        /// <summary>
        /// Gets or sets the name of the supported language. This is the same name as the IConfiguration section for the worker.
        /// </summary>
        [JsonProperty(PropertyName = "language")]
        public string Language { get; set; }

        /// <summary>
        /// Gets or sets the supported file extension type. Functions are registered with workers based on extension.
        /// </summary>
        [JsonProperty(PropertyName = "extensions")]
        public List<string> Extensions
        {
            get
            {
                return _extensions;
            }

            set
            {
                if (value != null)
                {
                    _extensions = value;
                }
            }
        }

        public override void ApplyDefaultsAndValidate()
        {
            if (workerDirectory == null)
            {
                throw new ArgumentNullException(nameof(workerDirectory));
            }
            Arguments = Arguments ?? new List<string>();
            WorkerDirectory = WorkerDirectory ?? workerDirectory;
            if (!string.IsNullOrEmpty(DefaultWorkerPath) && !Path.IsPathRooted(DefaultWorkerPath))
            {
                DefaultWorkerPath = Path.Combine(WorkerDirectory, DefaultWorkerPath);
            }
            if (string.IsNullOrEmpty(Language))
            {
                throw new ValidationException($"WorkerDescription {nameof(Language)} cannot be empty");
            }
            if (Extensions == null)
            {
                throw new ValidationException($"WorkerDescription {nameof(Extensions)} cannot be null");
            }
            if (string.IsNullOrEmpty(DefaultExecutablePath))
            {
                throw new ValidationException($"WorkerDescription {nameof(DefaultExecutablePath)} cannot be empty");
            }
            if (!string.IsNullOrEmpty(DefaultWorkerPath) && !File.Exists(DefaultWorkerPath))
            {
                throw new FileNotFoundException($"Did not find {nameof(DefaultWorkerPath)} for language: {Language}");
            }
        }
    }
}