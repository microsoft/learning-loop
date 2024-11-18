// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DecisionService.Common.Utils;
using Microsoft.DecisionService.Common.Storage;

namespace Microsoft.DecisionService.Common
{
    public class ModelExportManager
    {
        private readonly IBlobContainerClient container;
        private const int NbStagedModelAlwaysKept = 3;

        public bool AutoPublish { get; private set; }

        public int HistoryLength { get; private set; }

        public ModelExportManager(IBlobContainerClient container, bool autoPublish, int historyLength)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.AutoPublish = autoPublish;
            this.HistoryLength = Math.Max(historyLength, 1); // history cannot be negative
        }

        /// <summary>
        /// List all metadata ordered by descending creation date (most recent first)
        /// </summary>
        public virtual async Task<IEnumerable<ModelMetadata>> ListModelMetadataAsync(CancellationToken cancellation)
        {
            //get all exported blobs
            var blobs = await this.container.GetBlobsAsync(prefix: AzureBlobConstants.ExportedModelsDirectory,
                cancellationToken: cancellation); 

            //filter metadata blobs
            blobs = blobs.Where(blob => blob.Name.EndsWith(AzureBlobConstants.MetadataSuffix)).ToList();

            if (blobs.Count() == 0)
                return new List<ModelMetadata>();

            //build metadata list
            var metadataList = new List<ModelMetadata>();
            foreach (var blob in blobs)
            {
                var json = await this.container.GetBlobClient(blob.Name).DownloadAsync(); 
                var metadata = JsonConvert.DeserializeObject<ModelMetadata>(json.ToString());
                metadataList.Add(metadata);
            }

            //order by date
            return metadataList.OrderByDescending(m => m.CreationDate);
        }

        /// <summary>
        /// Get ExportedModel by Id
        /// </summary>
        public async Task<ExportedModel> GetExportedModelAsync(string modelId, CancellationToken cancellation)
        {
            var clientModelBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.ClientModelSuffix}");
            var trainerModelBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.TrainerModelSuffix}");
            var metadataBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.MetadataSuffix}");

            using (var clientModelStream = new MemoryStream())
            using (var trainerModelStream = new MemoryStream())
            {
                var downloadClientModelTask = clientModelBlob.DownloadToAsync(clientModelStream, cancellation);
                var downloadTrainerModelTask = trainerModelBlob.DownloadToAsync(trainerModelStream, cancellation);
                var downloadMetadataTask = metadataBlob.DownloadAsync(cancellation);

                await Task.WhenAll(downloadClientModelTask, downloadTrainerModelTask, downloadMetadataTask);

                return new ExportedModel(
                    clientModel: clientModelStream.ToArray(),
                    trainerModel: trainerModelStream.ToArray(),
                    metadata: JsonConvert.DeserializeObject<ModelMetadata>((await downloadMetadataTask).ToString()));
            }
        }

        /// <summary>
        /// Set model metadata
        /// </summary>
        public async Task SetModelMetadataAsync(string modelId, string jsonMetadata, CancellationToken cancellation)
        {
            var metadataBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.MetadataSuffix}");
            await metadataBlob.UploadAsync(BinaryData.FromString(jsonMetadata), cancellation);
        }

        /// <summary>
        /// Set model metadata
        /// </summary>
        public async Task SetModelMetadataAsync(string modelId, ModelMetadata metadata, CancellationToken cancellation)
        {
            var json = JsonConvert.SerializeObject(metadata);
            await this.SetModelMetadataAsync(modelId, json, cancellation);
        }

        /// <summary>
        /// Delete model
        /// </summary>
        public virtual async Task DeleteExportedModelAsync(string modelId, CancellationToken cancellation)
        {
            var inferenceModelBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.ClientModelSuffix}");
            var trainerModelBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.TrainerModelSuffix}");
            var metadataBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{modelId}.{AzureBlobConstants.MetadataSuffix}");

            var deleteInferenceModelBlobTask = inferenceModelBlob == null ? Task.CompletedTask : inferenceModelBlob.DeleteIfExistsAsync(null, cancellationToken: cancellation);
            var deleteTrainerModelBlobTask = trainerModelBlob == null ? Task.CompletedTask : trainerModelBlob.DeleteIfExistsAsync(null, cancellationToken: cancellation);
            var deleteMetadataBlobTask = metadataBlob == null ? Task.CompletedTask : metadataBlob.DeleteIfExistsAsync(null, cancellationToken: cancellation);

            await Task.WhenAll(deleteInferenceModelBlobTask, deleteTrainerModelBlobTask, deleteMetadataBlobTask);
        }

        /// <summary>
        /// Try to update "current" and "currenttrainer" models with prediction and trainer models repectively
        /// </summary>
        public virtual async Task<bool> TryUpdateCurrentAsync(string newModelId, CancellationToken cancellation)
        {
            var oldClientModelBlob = this.container.GetBlobClient(AzureBlobConstants.ClientModelBlobName);
            var newClientModelBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{newModelId}.{AzureBlobConstants.ClientModelSuffix}");
            var oldTrainerModelBlob = this.container.GetBlobClient(AzureBlobConstants.TrainerModelBlobName);
            var newTrainerModelBlob = this.container.GetBlobClient($"{AzureBlobConstants.ExportedModelsDirectory}/{newModelId}.{AzureBlobConstants.TrainerModelSuffix}");

            //sanity
            if (oldClientModelBlob == null || newClientModelBlob == null || newTrainerModelBlob == null || oldTrainerModelBlob == null)
                return false;

            if (!await newClientModelBlob.ExistsAsync(cancellation) || !await newTrainerModelBlob.ExistsAsync(cancellation))
                return false;//model id does not exist

            await oldClientModelBlob.StartCopyFromAsync(newClientModelBlob);
            await oldTrainerModelBlob.StartCopyFromAsync(newTrainerModelBlob);

            return true;
        }

        /// <summary>
        /// Clean models:
        /// * always keep the last 'nbStagedModelKept' models
        /// * keep protected (flagged as 'do_not_delete') models unless their are older than history length
        /// </summary>
        public async Task CleanStagedModelsAsync(CancellationToken cancellation)
        {
            IEnumerable<ModelMetadata> metadatas = await this.ListModelMetadataAsync(cancellation);

            if (metadatas.Count() <= NbStagedModelAlwaysKept)
                return;

            //keep only last 3 models, delete others except those protected
            await Task.WhenAll(metadatas
                .Skip(NbStagedModelAlwaysKept)
                .Where(m => !m.SavedInHistory)
                .Select(m => this.DeleteExportedModelAsync(m.ModelId, cancellation)));

            //delete models that are older than history length (in days)
            await Task.WhenAll(metadatas
                .Skip(NbStagedModelAlwaysKept)
                .Where(m => m.CreationDate < DateTimeOpsInputValidation.SafeSubtractDays(DateTime.UtcNow, this.HistoryLength))
                .Select(m => this.DeleteExportedModelAsync(m.ModelId, cancellation)));
        }
    }
}
