// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common
{
    public class ExportedModel
    {
        public byte[] ClientModel { get; private set; }
        public byte[] TrainerModel { get; private set; }
        public ModelMetadata Metadata { get; private set; }

        public ExportedModel(byte[] clientModel, byte[] trainerModel, ModelMetadata metadata)
        {
            this.ClientModel = clientModel ?? throw new ArgumentNullException("clientModel is null");
            this.TrainerModel = trainerModel ?? throw new ArgumentNullException("trainerModel is null");
            this.Metadata = metadata ?? throw new ArgumentNullException("metadata is null");
        }
    }
}
