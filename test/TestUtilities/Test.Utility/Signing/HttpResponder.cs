// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Test.Utility.Signing
{
    public abstract class HttpResponder : IHttpResponder
    {
        public abstract Uri Url { get; }

        public abstract Task RespondAsync(HttpContext context);

        protected static bool IsGet(HttpRequest request)
        {
            return string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase);
        }

        protected static bool IsPost(HttpRequest request)
        {
            return string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase);
        }

        protected static byte[] ReadRequestBody(HttpRequest request)
        {
            using (var reader = new BinaryReader(request.Body))
            {
                return reader.ReadBytes((int)request.ContentLength.Value);
            }
        }

        protected static void WriteResponseBody(HttpResponse response, byte[] bytes)
        {
            response.ContentLength = bytes.Length;

            using (var writer = new BinaryWriter(response.Body))
            {
                writer.Write(bytes);
            }
        }
    }
}