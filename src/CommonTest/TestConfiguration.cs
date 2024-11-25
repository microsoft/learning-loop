// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CommonTest
{
    public static class TestConfiguration
    {

        public static string TryGet(TestContext context, string name)
        {
            if (context != null && context.Properties != null &&
                context.Properties.Contains(name))
            {
                var value = context.Properties[name].ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

         public static string Get(TestContext context, string name)
        {
            var value = TryGet(context, name);
            if (value == null)
            {
                throw new Exception($"Could not get configuration value for: '{name}'\n" +
                                $"Make sure to update test.runsettings with the name and value");
            }

            return value;
        }

    }
}
