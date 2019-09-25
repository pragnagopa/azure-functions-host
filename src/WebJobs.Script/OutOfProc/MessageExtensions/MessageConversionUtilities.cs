// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    internal static class MessageConversionUtilities
    {
        internal static bool IsMediaTypeOctetOrMultipart(MediaTypeHeaderValue mediaType)
        {
            return mediaType != null && (string.Equals(mediaType.MediaType, "application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
                            mediaType.MediaType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static byte[] RequestBodyToBytes(HttpRequest request)
        {
            var length = Convert.ToInt32(request.ContentLength);
            var bytes = new byte[length];
            request.Body.Read(bytes, 0, length);
            request.Body.Position = 0;
            return bytes;
        }

        internal static string GetStringRepresentationOfBody(HttpRequest request)
        {
            string result;
            using (StreamReader reader = new StreamReader(request.Body, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                result = reader.ReadToEnd();
            }
            request.Body.Position = 0;
            return result;
        }
    }
}