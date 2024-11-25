// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.TimeProvider
{
    public class IncrementingTimeProvider : ITimeProvider
    {
        private DateTime startDateTime;
        private TimeSpan tsIncrement;
        private int idx;
        private TaskCompletionSource<bool> delayTrig;

        public IncrementingTimeProvider(DateTime start, TimeSpan tsIncrement)
        {
            startDateTime = start;
            this.tsIncrement = tsIncrement;
            this.delayTrig = new TaskCompletionSource<bool>();
        }

        public DateTime UtcNow
        {
            get
            {
                DateTime ret = startDateTime.Add(TimeSpan.FromTicks(idx * tsIncrement.Ticks));
                Interlocked.Increment(ref idx);
                return ret;
            }

            set
            {
                startDateTime = value;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "Method is Task creator no async itself.")]
        private static Task AsTask(CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<object>();
            ct.Register(() => tcs.TrySetCanceled());
            return tcs.Task;
        }

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            // Wait on an external trigger
            var trigTask = await Task.WhenAny(delayTrig.Task,AsTask(cancellationToken));

            // check if cancel triggered
            if (trigTask != delayTrig.Task)
                return;

            delayTrig = new TaskCompletionSource<bool>();

            // Next time UtcNow gets called, it would have moved the time up by delay ms
            var numters = delay.TotalMilliseconds / tsIncrement.TotalMilliseconds;
            Interlocked.Add(ref idx, (int)numters);
        }

        public void TriggerDelay()
        {
            TriggerDelay(TimeSpan.Zero);
        }

        public void TriggerDelay(TimeSpan addTimeSpan)
        {
            var numters = addTimeSpan.TotalMilliseconds / tsIncrement.TotalMilliseconds;
            Interlocked.Add(ref idx, (int)numters);
            this.delayTrig.SetResult(true);
        }
    }
}