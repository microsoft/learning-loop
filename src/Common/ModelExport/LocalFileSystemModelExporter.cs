// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Trainer.ModelExport
{
    public class LocalFileSystemModelExporter : IModelExporter
    {
        private const string TrainerModelPathEnvironmentVariableName = "TrainerModelPath";
        private const string ClientModelPathEnvironmentVariableName = "ClientModelPath";
        private const string DecoderModelPathEnvironmentVariableName = "DecoderModelPath";
        private const string MetadataPathEnvironmentVariableName = "MetadataPath";

        private readonly ILogger logger;
        private readonly string clientModelPath;
        private readonly string trainerModelPath;
        private readonly string decoderModelPath;
        private readonly string metadataPath;

        public LocalFileSystemModelExporter(ILogger traceSession, string clientModelPath, string trainerModelPath, string metadataPath, string decoderModelPath=null)
        {
            this.logger = traceSession;
            this.clientModelPath = clientModelPath;
            this.trainerModelPath = trainerModelPath;
            this.metadataPath = metadataPath;
            this.decoderModelPath = decoderModelPath;
        }

        public LocalFileSystemModelExporter()
        {
            this.clientModelPath = Environment.GetEnvironmentVariable(ClientModelPathEnvironmentVariableName);
            this.trainerModelPath = Environment.GetEnvironmentVariable(TrainerModelPathEnvironmentVariableName);
            this.metadataPath = Environment.GetEnvironmentVariable(MetadataPathEnvironmentVariableName);
            this.decoderModelPath = Environment.GetEnvironmentVariable(DecoderModelPathEnvironmentVariableName);

            if (this.clientModelPath == null || this.trainerModelPath == null || this.metadataPath == null)
            {
                string msg = @"Could not find path to export models. Please set environment varialbes {0}, {1} and {2}.";
                throw new ArgumentNullException(
                    string.Format(msg, TrainerModelPathEnvironmentVariableName, ClientModelPathEnvironmentVariableName, MetadataPathEnvironmentVariableName));
            }

        }

        private async Task UploadClientModelFromByteArrayAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
        {
            this.logger?.LogInformation($"Export client model to {this.clientModelPath}", "LocalFileSystemModelExporter");
            await ExportModelAsync(buffer, index, count, this.clientModelPath, cancellationToken);
        }

        private async Task UploadTrainerModelFromByteArrayAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
        {
            this.logger?.LogInformation($"Export trainer model to {this.trainerModelPath}", "LocalFileSystemModelExporter");
            await ExportModelAsync(buffer, index, count, this.trainerModelPath, cancellationToken);
        }
        private async Task UploadMetadataAsync(string jsonMetadata, CancellationToken cancellationToken)
        {
            this.logger?.LogInformation($"Export metadata to {this.metadataPath}", "LocalFileSystemModelExporter");
            var buffer = System.Text.Encoding.UTF8.GetBytes(jsonMetadata);
            await ExportModelAsync(buffer, 0, buffer.Length, this.metadataPath, cancellationToken);
        }


        private async Task ExportModelAsync(byte[] model, int startIndex, int count, string path, CancellationToken cancellationToken)
        {
            try
            {
                Directory.CreateDirectory(new FileInfo(path).DirectoryName);
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    await fs.WriteAsync(model, startIndex, count, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                this.logger?.LogInformation($"Model export to file location {path} was cancelled.", "LocalFileSystemModelExporter.Exit");
            }
        }

        public async Task UploadAsync(byte[] inferenceModel, byte[] trainerModel, string jsonMetadata, CancellationToken cancellation) {
            byte[] buffer = Encoding.UTF8.GetBytes(jsonMetadata);

            List<Task> tasks = new List<Task>
            {
                ExportModelAsync(inferenceModel, 0, inferenceModel.Length, this.clientModelPath, cancellation),
                ExportModelAsync(trainerModel, 0, trainerModel.Length, this.trainerModelPath, cancellation),
                ExportModelAsync(buffer, 0, buffer.Length, this.metadataPath, cancellation)
            };
           
            this.logger?.LogInformation($"Export client model, trainer model and metadata resp. to {this.clientModelPath}, {this.trainerModelPath}, and {this.metadataPath}", "LocalFileSystemModelExporter");

            await Task.WhenAll(tasks);
        }
    }
}
