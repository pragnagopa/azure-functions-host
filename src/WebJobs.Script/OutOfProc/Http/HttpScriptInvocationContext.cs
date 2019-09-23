// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    public class HttpScriptInvocationContext
    {
        public JObject Metadata { get; set; }

        public JObject Data { get; set; }
    }
}
