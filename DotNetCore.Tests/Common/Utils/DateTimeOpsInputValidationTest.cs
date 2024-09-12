// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DotNetCore.Tests.Common.Utils
{
    [TestClass]
    public class DateTimeOpsInputValidationTest
    {
        private readonly int valueMax = int.MaxValue;
        private readonly int valueMin = int.MinValue;
        private readonly int valuePositive = 12000;
        private readonly int valueNegative = -12000;

        [TestMethod]
        public void AddDays_SafeSubtractDays()
        {
            Assert.AreEqual(DateTime.MinValue, DateTimeOpsInputValidation.SafeSubtractDays(DateTime.UtcNow.Date, valueMax));
            Assert.AreEqual(DateTime.MaxValue, DateTimeOpsInputValidation.SafeSubtractDays(DateTime.UtcNow, valueMin));
            Assert.AreEqual(DateTime.UtcNow.Date.AddDays(-valuePositive), DateTimeOpsInputValidation.SafeSubtractDays(DateTime.UtcNow.Date, valuePositive));
            Assert.AreEqual(DateTime.UtcNow.AddDays(-valueNegative).Date, DateTimeOpsInputValidation.SafeSubtractDays(DateTime.UtcNow, valueNegative).Date);
        }

        [TestMethod]
        public void AddDays_SafeAddDays()
        {
            Assert.AreEqual(DateTime.MaxValue, DateTimeOpsInputValidation.SafeAddDays(DateTime.UtcNow.Date, valueMax));
            Assert.AreEqual(DateTime.MinValue, DateTimeOpsInputValidation.SafeAddDays(DateTime.UtcNow, valueMin));
            Assert.AreEqual(DateTime.UtcNow.Date.AddDays(valuePositive), DateTimeOpsInputValidation.SafeAddDays(DateTime.UtcNow.Date, valuePositive));
            Assert.AreEqual(DateTime.UtcNow.AddDays(valueNegative).Date, DateTimeOpsInputValidation.SafeAddDays(DateTime.UtcNow, valueNegative).Date);
        }

    }
}
