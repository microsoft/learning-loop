// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;

namespace Microsoft.DecisionService.Common.Error
{
    internal class HttpStatusCodeAttribute : Attribute
    {
        private readonly HttpStatusCode httpStatusCode;
        public HttpStatusCodeAttribute(HttpStatusCode httpStatusCode)
        {
            this.httpStatusCode = httpStatusCode;
        }

        public HttpStatusCode GetHttpStatusCode()
        {
            return this.httpStatusCode;
        }

    }
}