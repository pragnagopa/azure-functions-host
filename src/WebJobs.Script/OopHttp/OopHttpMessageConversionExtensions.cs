// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Script.OopHttp
{
    internal static class OopHttpMessageConversionExtensions
    {
        private static readonly JsonSerializerSettings _datetimeSerializerSettings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };

        public static Task<object> ToObject(this HttpResponseMessage responseMessage)
        {
            return Utilities.ConvertFromHttpMessageToExpando(responseMessage);
        }

        //public static TypedData ToHttpOppHeader(this object value, ILogger logger)
        //{
        //    TypedData typedData = new TypedData();
        //    if (value == null)
        //    {
        //        return typedData;
        //    }

        //    if (value is byte[] arr)
        //    {
        //        typedData.Bytes = ByteString.CopyFrom(arr);
        //    }
        //    else if (value is JObject jobj)
        //    {
        //        typedData.Json = jobj.ToString();
        //    }
        //    else if (value is string str)
        //    {
        //        typedData.String = str;
        //    }
        //    else if (value is HttpRequest request)
        //    {
        //        typedData = request;
        //    }
        //    else
        //    {
        //        typedData = value.ToRpcDefault();
        //    }

        //    return typedData;
        //}
    }
}