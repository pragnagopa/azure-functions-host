// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc.Http
{
    public class HttpRequestMessageConverters
    {
        internal static JObject ConvertHttpRequestToJObject(HttpRequest request)
        {
            var jObjectHttp = new JObject();
            jObjectHttp["Url"] = $"{(request.IsHttps ? "https" : "http")}://{request.Host.ToString()}{request.Path.ToString()}{request.QueryString.ToString()}"; // [http|https]://{url}{path}{query}
            jObjectHttp["Method"] = request.Method.ToString();
            if (request.Query != null)
            {
                jObjectHttp["Query"] = ConvertQueryCollectionToJson(request.Query);
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
            if (request.HttpContext?.User?.Identities != null)
            {
                jObjectHttp["Identities"] = JsonConvert.SerializeObject(request.HttpContext.User.Identities);
            }

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

        internal static string ConvertQueryCollectionToJson(IQueryCollection query)
        {
            return JsonConvert.SerializeObject(ConvertQueryCollectionToDictionary(query));
        }

        internal static IDictionary<string, string> ConvertQueryCollectionToDictionary(IQueryCollection query)
        {
            var queryParamsDictionary = new Dictionary<string, string>();
            foreach (var key in query.Keys)
            {
                query.TryGetValue(key, out StringValues value);
                queryParamsDictionary.Add(key, value.ToString());
            }
            return queryParamsDictionary;
        }
    }
}
