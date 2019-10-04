﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Script.OutOfProc
{
    public static class HttpInvokerConstants
    {
        public const string InvocatoinIdHeaderName = "X-Azure-Functions-InvocationId";
        public const string HostVersionHeader = "X-Azure-Functions-HostVersion";
        public const string UserAgentHeaderValue = "Azure-Functions-Host";
    }
}
