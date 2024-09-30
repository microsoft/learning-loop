// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.OnlineTrainer.Data
{
    public class ModelExportEvent
    {
        public byte[] ClientModelData { get; set; }

        public byte[] TrainerModelData { get; set; }

        public int NumberOfEventsLearnedSinceLastExport { get; set; }

        public string JsonMetadata { get; set; }
    }
}
