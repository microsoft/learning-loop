// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.Common.Trainer.ModelExport
{
    /// <summary>
    /// Class that exports models to Azure blob storage
    /// </summary>
    public class BlobModelExporter : IModelExporter
    {
        private readonly ILogger traceSession;
        private readonly IBlobContainerClient container;
        private readonly ModelExportManager manager;

        public BlobModelExporter(ILogger traceSession, IBlobContainerClient container, ModelExportManager manager)
        {
            this.traceSession = traceSession;
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
        
        public async Task UploadAsync(byte[] inferenceModel, byte[] trainerModel, string jsonMetadata, CancellationToken cancellation)
        {
            //read the model id and use it as a blob base name
            string modelId;
            try
            {
                var metadata = JsonConvert.DeserializeObject<ModelMetadata>(jsonMetadata);
                modelId = metadata.ModelId;
                
                // TODO. Consider replacing this with another solution (it used to be in the sdk(
                // NameValidator.ValidateBlobName(modelId);
            }
            catch (Exception ex)
            {
                this.traceSession?.LogError(ex, "");
                throw;
            }

            var inferenceModelBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.ClientModelSuffix}");
            var trainerModelBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.TrainerModelSuffix}");
            var metadataBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.MetadataSuffix}");

            //upload models and metadata
            try
            {
                this.traceSession?.LogInformation("upload models and metadata in {path}", $"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.*");

                var tasks = new List<Task> {
                    inferenceModelBlob.UploadAsync(BinaryData.FromBytes(inferenceModel), cancellation),
                    trainerModelBlob.UploadAsync(BinaryData.FromBytes(trainerModel), cancellation),
                    metadataBlob.UploadAsync(BinaryData.FromString(jsonMetadata), cancellation)
                };
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                this.traceSession?.LogError(ex, "");
                return;
            }

            //override "current" model if needed
            if (manager.AutoPublish)
            {
                bool updateSucceeded = await this.manager.TryUpdateCurrentAsync(modelId, cancellation);
                if (updateSucceeded)
                    this.traceSession?.LogInformation("Current model was replaced successfully by model {modelId}", modelId);
                else
                    this.traceSession?.LogError("Current model was NOT replaced: model {modelId} not found", modelId);
            }
            else
            {
                this.traceSession?.LogInformation("export is manual: do not update {ClientModelBlobName} model", AzureBlobConstants.ClientModelBlobName);
            }

            //clean old models
            await this.manager.CleanStagedModelsAsync(cancellation);
        }
    }
}
