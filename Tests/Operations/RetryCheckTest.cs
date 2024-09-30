// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Trainer.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Tests.TimeProvider;

namespace Tests.Operations
{
    [TestClass]
    public class RetryCheckTest
    {
        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void RetryCheckTests_Increase()
        {
            TimeSpan timeProviderIncrement = TimeSpan.FromMinutes(1);
            var timeProvider = new IncrementingTimeProvider(new DateTime(2017, 7, 24, 0, 0, 0), timeProviderIncrement);
            var retryCheck = new RetryCheck(timeProvider);

            //increase wait time to be 2^1 seconds
            retryCheck.TryIncrease();
            Assert.AreEqual(1, retryCheck.RetryCount);
            Assert.AreEqual(new DateTime(2017, 7, 24, 0, 0, 0), retryCheck.LastCounterUpdateDate);
            Assert.AreEqual(TimeSpan.FromSeconds(2).TotalMilliseconds, retryCheck.RetryDuration);

            //increase wait time to be 2^1 * 2^5 seconds
            for (int i = 0; i < 5; i++)
            {
                retryCheck.TryIncrease();
            }

            Assert.AreEqual(6, retryCheck.RetryCount);
            Assert.AreEqual(new DateTime(2017, 7, 24, 0, 5, 0), retryCheck.LastCounterUpdateDate);
            Assert.AreEqual(TimeSpan.FromSeconds(64).TotalMilliseconds, retryCheck.RetryDuration);
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void RetryCheckTests_Reset()
        {
            TimeSpan timeProviderIncrement = TimeSpan.FromMinutes(1);
            var timeProvider = new IncrementingTimeProvider(new DateTime(2017, 7, 24, 0, 0, 0), timeProviderIncrement);
            var retryCheck = new RetryCheck(timeProvider);

            retryCheck.TryIncrease();
            retryCheck.Reset();

            Assert.AreEqual(0, retryCheck.RetryCount);
            Assert.AreEqual(DateTime.MinValue, retryCheck.LastCounterUpdateDate);
            Assert.AreEqual(TimeSpan.FromSeconds(1).TotalMilliseconds, retryCheck.RetryDuration);
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void RetryCheckTests_Check_Success()
        {
            TimeSpan timeProviderIncrement = TimeSpan.FromMinutes(1);
            var timeProvider = new IncrementingTimeProvider(new DateTime(2017, 7, 24, 0, 0, 0), timeProviderIncrement);
            var retryCheck = new RetryCheck(timeProvider);

            retryCheck.TryIncrease();

            Assert.IsTrue(retryCheck.ShouldRetry());
        }

        [TestMethod]
        [TestCategory("Decision Service/Online Trainer")]
        public void RetryCheckTests_Check_Wait()
        {
            TimeSpan timeProviderIncrement = TimeSpan.FromMinutes(1);
            var timeProvider = new IncrementingTimeProvider(new DateTime(2017, 7, 24, 0, 0, 0), timeProviderIncrement);
            var retryCheck = new RetryCheck(timeProvider);

            // to make sure wait time goes to 2^6 seconds which is more than 1 minute
            for (int i = 0; i < 6; i++)
            {
                retryCheck.TryIncrease();
            }

            Assert.IsFalse(retryCheck.ShouldRetry());
        }
    }
}
