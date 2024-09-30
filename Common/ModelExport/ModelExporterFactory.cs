// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.OnlineTrainer.Operations;
using Microsoft.Extensions.Logging;

namespace Microsoft.DecisionService.Common.Trainer.ModelExport
{
    public class ModelExporterFactory
    {
        public static IModelExporter Create(ModelExportBlockOptions options, ILogger logger)
        {
            logger?.LogInformation("setup export in tenant storage");
            var exportManager = new ModelExportManager(options.ContainerClient, options.ModelAutoPublish, options.StagedModelHistoryLength);
            return new BlobModelExporter(logger, options.ContainerClient, exportManager);
        }
    }
}
