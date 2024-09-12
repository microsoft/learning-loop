// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DecisionService.Common
{
    public class BatchBlockExOptions<T> : DataflowBlockOptions
    {
        /// <summary>
        /// Returns a function which returns the size of an item.
        /// </summary>
        public Func<T, int> MeasureItem { get; set; }

        /// <summary>
        /// Maximum time after which this block will flush
        /// </summary>
        public TimeSpan MaximumFlushLatency { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Time provider
        /// </summary>
        public ITimeProvider TimeProvider { get; set; }

        /// <summary>
        /// Guaranteed to be called in-order of events and only once per item!
        /// </summary>
        public Predicate<T> StartNewPredicate { get; set; } = _ => false;

        /// <summary>
        /// When this returns true, the item is included, but the batch is flushed out.
        /// </summary>
        public Predicate<T> IsFlushItem { get; set; } = _ => false;
    }
}
