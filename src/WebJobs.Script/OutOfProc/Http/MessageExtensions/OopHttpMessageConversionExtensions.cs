﻿// Copyright (c) .NET Foundation. All rights reserved.
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

        public static string ToHttpOppHeader(this object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (value is JObject jobj)
            {
               return jobj.ToString();
            }
            else if (value is string str)
            {
                return str;
            }
            else if (value is HttpRequest request)
            {
                return GetStringRepresentationOfBody(request);
            }

            return value.ToString();
        }

        private static string GetStringRepresentationOfBody(HttpRequest request)
        {
            string result;
            using (StreamReader reader = new StreamReader(request.Body, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                result = reader.ReadToEnd();
            }
            // request.Body.Position = 0;
            return result;
        }
    }
}