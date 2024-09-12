// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCore.Tests
{
    public static class AssertExtensions
    {
        /// <summary>
        /// Asserts that two objects of type <typeparamref name="T"/> are equal
        /// by comparing the values of their public properties.
        /// </summary>
        /// <param name="assert">This assert.</param>
        /// <param name="expected">The expected <typeparamref name="T"/></param>
        /// <param name="actual">The actual <typeparamref name="T"/></param>
        /// <typeparam name="T">The type to check for equality.</typeparam>
        public static void ArePropertyEqual<T>(this Assert assert, T expected, T actual)
        {
            Assert.IsNotNull(expected);
            Assert.IsNotNull(actual);
            PropertyInfo[] propertyInfos = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo pInfo in propertyInfos)
            {
                if (!pInfo.CanRead) continue;

                MethodInfo getMethod = pInfo.GetGetMethod(false);
                if (getMethod == null) continue;

                Assert.AreEqual(pInfo.GetValue(expected, null), pInfo.GetValue(actual, null), pInfo.Name);
            }
        }
    }
}
