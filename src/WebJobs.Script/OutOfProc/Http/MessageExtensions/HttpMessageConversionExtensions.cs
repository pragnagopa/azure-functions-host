// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    internal static class HttpMessageConversionExtensions
    {
        public static JToken ToJToken(this object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (value is byte[] arr)
            {
                return value.ToJToken();
            }
            else if (value is JObject jobj)
            {
                return jobj;
            }
            else if (value is string || value is int || value is float || value is double)
            {
                return value.ToString();
            }
            else if (value is HttpRequest request)
            {
                return request.ToJObjectHttp();
            }
            else if (value.GetType().IsArray)
            {
                return JArray.FromObject(value);
            }
            else
            {
                try
                {
                    return JsonConvert.SerializeObject(value);
                }
                catch
                {
                    return value.ToString();
                }
            }
        }

        internal static JObject ToJObjectHttp(this HttpRequest request)
        {
            var jObjectHttp = new JObject();
            jObjectHttp["Url"] = $"{(request.IsHttps ? "https" : "http")}://{request.Host.ToString()}{request.Path.ToString()}{request.QueryString.ToString()}"; // [http|https]://{url}{path}{query}
            jObjectHttp["Method"] = request.Method.ToString();
            if (request.Query != null)
            {
                jObjectHttp["Query"] = request.Query.QueryToJson();
            }
            if (request.Headers != null)
            {
                jObjectHttp["Headers"] = JObject.FromObject(request.Headers);
            }
            if (request.HttpContext.Items.TryGetValue(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, out object routeData))
            {
                Dictionary<string, object> parameters = (Dictionary<string, object>)routeData;
                if (parameters != null)
                {
                    jObjectHttp["Params"] = JObject.FromObject(parameters);
                }
            }

            // TODO: parse ClaimsPrincipal if exists
            //if (request.HttpContext?.User?.Identities != null)
            //{
            //    jObjectHttp["Identities"] = JObject.FromObject(request.HttpContext.User.Identities);
            //}

            // parse request body as content-type
            if (request.Body != null && request.ContentLength > 0)
            {
                if (MediaTypeHeaderValue.TryParse(request.ContentType, out MediaTypeHeaderValue mediaType) && MessageConversionUtilities.IsMediaTypeOctetOrMultipart(mediaType))
                {
                    jObjectHttp["Body"] = MessageConversionUtilities.RequestBodyToBytes(request);
                }
                else
                {
                    jObjectHttp["Body"] = MessageConversionUtilities.GetStringRepresentationOfBody(request);
                }
            }

            return jObjectHttp;
        }

        internal static string QueryToJson(this IQueryCollection query)
        {
            var dict = new Dictionary<string, string>();
            foreach (var key in query.Keys)
            {
                query.TryGetValue(key, out StringValues value);
                dict.Add(key, value.ToString());
            }
            return JsonConvert.SerializeObject(dict);
        }
    }
}