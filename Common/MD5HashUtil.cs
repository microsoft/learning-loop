// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography;

namespace Microsoft.DecisionService.Common
{
    public static class MD5HashUtil
    {
        public static string GetMd5Hash(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            {
#pragma warning disable CA5351
                using (var md5 = MD5.Create())
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream));
                }
#pragma warning restore CA5351
            }
        }

        public static string GetMd5Hash(byte[] bytes)
        {
#pragma warning disable CA5351
            using (var md5 = MD5.Create())
            {
                return Convert.ToBase64String(md5.ComputeHash(bytes));
            }
#pragma warning restore CA5351
        }
    }
}
