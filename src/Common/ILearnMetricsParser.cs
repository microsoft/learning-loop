// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Trainer
{
    /// <summary>
    /// Parses <see cref="ILearnMetrics"/>.
    /// </summary>
    public interface ILearnMetricsParser
    {
        /// <summary>
        /// Parses <paramref name="path"/> and extracts an <see cref="ILearnMetrics"/>.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>
        /// The <see cref="ILearnMetrics"/> parsed from <paramref name="path"/>.
        /// </returns>
        ILearnMetrics Parse(string path);

        /// <summary>
        /// Gets default <see cref="ILearnMetrics"/>.
        /// </summary>
        /// <returns>The default <see cref="ILearnMetrics"/>.</returns>
        ILearnMetrics Default();
    }
}
