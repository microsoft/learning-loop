// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Trainer
{
    /// <summary>
    /// Creates instance of <see cref="ILearnMetricsParser"/>.
    /// </summary>
    public static class LearnMetricsParserFactory
    {
        /// <summary>
        /// Creates an <see cref="ILearnMetricsParser"/> for the given problem type.
        /// </summary>
        /// <param name="type">The problem type.</param>
        /// <returns>The parser that is appropriate for the problem type.</returns>
        /// <exception cref="NotImplementedException">Thrown if the problem type is not supported/implemented yet.</exception>
        public static ILearnMetricsParser Create(ProblemType type)
        {
            switch (type)
            {
                case ProblemType.CB:
                    return new VwLearnMetricsFileParser<VwCbLearnMetrics>();
                case ProblemType.CCB:
                    return new VwLearnMetricsFileParser<VwCcbLearnMetrics>();
                default:
                    throw new NotImplementedException($"Learn metrics support has not been implemented for {type}");
            }
        }
    }
}
