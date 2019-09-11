// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.WebJobs.Script.OopHttp
{
    internal static class Utilities
    {
        public static async Task<object> ConvertFromHttpMessageToExpando(HttpResponseMessage inputMessage)
        {
            if (inputMessage == null)
            {
                return null;
            }

            dynamic expando = new ExpandoObject();
            expando.method = inputMessage.RequestMessage.Method;
            // expando.query = inputMessage.Query as IDictionary<string, string>;
            expando.statusCode = inputMessage.StatusCode;
            expando.headers = inputMessage.Headers.ToDictionary(p => p.Key, p => (object)p.Value);

            if (inputMessage.Content != null)
            {
                expando.body = await inputMessage.Content.ReadAsStringAsync();
            }
            //return expando;
            return expando.body;
        }
    }
}