// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

namespace Microsoft.DecisionService.Common
{
    /// <summary>
    /// Encodes and decodes strings in ASCII character set to/from Base 64.
    /// </summary>
    public static class AsciiBase64Converter
    {
        /// <summary>
        /// Encodes a string in the ASCII character set to Base 64.
        /// </summary>
        /// <param name="s">The string to encode.</param>
        /// <returns>The encoded string.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="s"/> is null.</exception>
        public static string Encode(string s)
        {
            return Convert.ToBase64String(Encoding.ASCII.GetBytes(s));
        }

        /// <summary>
        /// Decodes a base 64 string in the ASCII character set.
        /// </summary>
        /// <param name="s">The string to decode.</param>
        /// <returns>The decoded string. null is <paramref name="s"/> was not able to be decoded.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="s"/> is null.</exception>
        /// <exception cref="FormatException">When <paramref name="s"/> is not in a format that can be decoded.</exception>
        public static string Decode(string s)
        {
            return Encoding.ASCII.GetString(Convert.FromBase64String(s));
        }
    }
}
