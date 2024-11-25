// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Trainer.Operations
{
    public class RetryCheck
    {
        private readonly int maxRetryCount = 10;
        private readonly int exponentialBackoffMultiplier = 2;
        private readonly double initRetryUnit = TimeSpan.FromSeconds(1).TotalMilliseconds;
        private readonly ITimeProvider timeProvider;

        public int RetryCount { private set; get; }
        public double RetryDuration { private set; get; }
        public DateTime LastCounterUpdateDate { private set; get; }

        public RetryCheck(ITimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
            this.RetryCount = 0;
            this.RetryDuration = this.initRetryUnit;
        }

        public bool TryIncrease()
        {
            this.LastCounterUpdateDate = this.timeProvider.UtcNow;
            if (RetryCount >= maxRetryCount)
            {
                return false;
            }
            this.RetryCount++;
            this.RetryDuration *= exponentialBackoffMultiplier;
            return true;
        }

        public void Reset()
        {
            this.RetryCount = 0;
            this.RetryDuration = this.initRetryUnit;
            this.LastCounterUpdateDate = DateTime.MinValue;
        }

        public bool ShouldRetry()
        {
            return this.timeProvider.UtcNow.Subtract(this.LastCounterUpdateDate).TotalMilliseconds >= this.RetryDuration;
        }
    }
}
