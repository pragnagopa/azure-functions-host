﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    public static class HttpInvokerConstants
    {
        public const string InvocationIdHeaderName = "X-Azure-Functions-InvocationId";
        public const string HostVersionHeaderName = "X-Azure-Functions-HostVersion";
        public const string UserAgentHeaderValue = "Azure-Functions-Host";
    }
}
