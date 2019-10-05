// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Script.Description;

namespace Microsoft.Azure.WebJobs.Script.Extensions
{
    public static class FunctionMetadataExtensions
    {
        public static bool IsSimpleHttpTriggerFunction(this FunctionMetadata functionMetadata)
        {
            if (functionMetadata == null)
            {
                throw new ArgumentNullException(nameof(functionMetadata));
            }
            if (functionMetadata.InputBindings.Count() != 1 || functionMetadata.OutputBindings.Count() != 1)
            {
                return false;
            }

            BindingMetadata inputBindingMetadata = functionMetadata.InputBindings.ElementAt(0);
            BindingMetadata outputBindingMetadata = functionMetadata.OutputBindings.ElementAt(0);
            if (string.Compare("httptrigger", inputBindingMetadata.Type, StringComparison.OrdinalIgnoreCase) == 0 &&
                string.Compare("http", outputBindingMetadata.Type, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            return false;
        }
    }
}