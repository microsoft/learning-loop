// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Trainer.ModelExport
{
    public interface IModelExporter
    {
        /// <summary>
        /// Upload the models and the related metadata
        /// </summary>
        /// <param name="inferenceModel">the inference model: can predict but cannot continue the training</param>
        /// <param name="trainerModel">the training model: can predict and continue the training</param>
        /// <param name="jsonMetadata">the metadata (JSON)</param>
        /// <param name="cancellation">the cancellation token</param>
        Task UploadAsync(byte[] inferenceModel, byte[] trainerModel, string jsonMetadata, CancellationToken cancellation);
    }
}