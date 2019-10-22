// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.Azure.WebJobs.Script.Description;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Script
{
    public interface IFunctionMetadataProvider
    {
        ImmutableDictionary<string, ImmutableArray<string>> FunctionErrors { get; }

        ImmutableDictionary<string, JObject> FunctionConfigs { get; }

        ImmutableArray<FunctionMetadata> GetFunctionMetadata(bool forceRefresh = false);
    }
}