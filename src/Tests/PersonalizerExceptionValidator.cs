// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Error;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Tests
{
    public static class PersonalizerExceptionValidator
    {
        public static async Task ExpectPersonalizerExceptionAsync(Func<Task> func, PersonalizerErrorCode errorCode, string errorMessage = null)
        {
            errorMessage ??= errorCode.GetDescription();
            try
            {
                await func();
                Assert.IsTrue(false);
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(PersonalizerException));
                var ex1 = ex as PersonalizerException;
                Assert.AreEqual(errorCode, ex1.PersonalizerErrorCode);
                string actualErrorMessage = ex1.InnerException?.Message ?? ex1.Message;
                Assert.AreEqual(errorMessage, actualErrorMessage);
            }
        }
    }
}
