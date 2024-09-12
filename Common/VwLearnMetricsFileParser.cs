// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using Microsoft.DecisionService.Common.Utils.FileSystem;
using Newtonsoft.Json;

namespace Microsoft.DecisionService.Common.Trainer
{
    /// <summary>
    /// Parses <see cref="ILearnMetrics"/> from a metrics file output from VW.
    /// </summary>
    /// <typeparam name="TProblemSpecificMetric">The problem-specific metric type/class</typeparam>
    public class VwLearnMetricsFileParser<TProblemSpecificMetric> : ILearnMetricsParser
        where TProblemSpecificMetric : ILearnMetrics, new()
    {
        public ILearnMetrics Parse(string path)
        {
            PathUtils.CheckPath(path);

            var serializer = new JsonSerializer();
            using StreamReader file = File.OpenText(path);
            return (TProblemSpecificMetric) serializer.Deserialize(file, typeof(TProblemSpecificMetric)) ?? Default();
        }

        public ILearnMetrics Default()
        {
            return new TProblemSpecificMetric();
        }
    }
}
