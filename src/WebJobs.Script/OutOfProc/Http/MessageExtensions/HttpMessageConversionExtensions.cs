// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Script.OopHttp
{
    internal static class HttpMessageConversionExtensions
    {
        private static readonly JsonSerializerSettings _datetimeSerializerSettings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };

        public static Task<object> ToObject(this HttpResponseMessage responseMessage)
        {
            return HttpMessageExtensionUtilities.ConvertFromHttpMessageToExpando(responseMessage);
        }

        public static string ToHttpHeader(this object value)
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