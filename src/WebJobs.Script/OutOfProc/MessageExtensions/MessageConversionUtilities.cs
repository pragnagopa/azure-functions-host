// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http.Headers;

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    internal static class MessageConversionUtilities
    {
        internal static bool IsMediaTypeOctetOrMultipart(MediaTypeHeaderValue mediaType)
        {
            return mediaType != null && (string.Equals(mediaType.MediaType, "application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
                            mediaType.MediaType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}