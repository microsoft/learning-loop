// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Error
{
    /// <summary>
    /// Provides extension methods for Exception handling.
    /// </summary>
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Determines whether the specified exception or any of its inner exceptions is of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of exception to check for.</typeparam>
        /// <param name="e">The exception to check.</param>
        /// <returns>True if the exception or any of its inner exceptions is of type T; otherwise, false.</returns>
        public static bool IsExceptionOf<T>(this Exception e) where T : Exception
        {
            while (e != null)
            {
                if (e is T)
                {
                    return true;
                }
                e = e.InnerException;
            }
            return false;
        }
    }
}
