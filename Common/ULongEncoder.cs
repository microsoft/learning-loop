// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

namespace Microsoft.DecisionService.Common
{
    /// <summary>
    /// Encode ulongs (e.g. timespan total second values) a bit more efficient
    /// </summary>
    public static class ULongEncoder
    {
        public static readonly string ValidCharsRegex = "[0-9a-zA-Z]+";

        private const ulong baseCount = 10 + 26 + 26;

        private static char ToChar(int val)
        {
            if (val < 10)
                return (char)('0' + val);

            val -= 10;
            if (val < 26)
                return (char)('a' + val);

            val -= 26;
            if (val < 26)
                return (char)('A' + val);

            throw new ArgumentOutOfRangeException("Val is too large: " + val);
        }

        private static bool TryToVal(char c, out int val)
        {
            if (c >= '0' && c <= '9')
            {
                val = c - '0';
                return true;
            }

            if (c >= 'a' && c <= 'z')
            {
                val = c - 'a' + 10;
                return true;
            }

            if (c >= 'A' && c <= 'Z')
            {
                val = c - 'A' + 10 + 26;
                return true;
            }

            val = -1;

            return false;
        }

        public static string Encode(ulong value)
        {
            if (value == 0)
                return "0";

            var sb = new StringBuilder();
            while (value > 0)
            {
                // encoded least-to-most significant
                var remainder = (int)(value % baseCount);
                sb.Append(ToChar(remainder));

                value /= baseCount;
            }

            // reverse to have nice readability
            for (int i = 0, j = sb.Length - 1; i < sb.Length / 2; i++, j--)
            {
                char temp = sb[j];
                sb[j] = sb[i];
                sb[i] = temp;
            }

            return sb.ToString();
        }

        public static bool TryDecode(string encoded, out ulong value)
        {
            value = 0;

            for (var i = 0; i < encoded.Length; i++)
            {
                var ch = encoded[i];
                if (!TryToVal(ch, out int val))
                    return false;

                value = (value * baseCount) + (ulong)val;
            }

            return true;
        }
    }
}
