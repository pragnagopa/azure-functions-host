// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    public abstract class WorkerDescription
    {
        /// <summary>
        /// Gets or sets the default executable path.
        /// </summary>
        public string DefaultExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the default path to the worker (relative to the bin/workers/{language} directory)
        /// </summary>
        public string DefaultWorkerPath { get; set; }

        /// <summary>
        /// Gets or sets the default base directory for the worker
        /// </summary>
        public string WorkerDirectory { get; set; }

        /// <summary>
        /// Gets or sets the default path to the worker (relative to the bin/workers/{language} directory)
        /// </summary>
        public List<string> Arguments { get; set; }

        public virtual void ApplyDefaultsAndValidate()
    }
}