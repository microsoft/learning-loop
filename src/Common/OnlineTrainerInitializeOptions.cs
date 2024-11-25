// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Trainer
{
    public sealed class OnlineTrainerInitializeOptions
    {
        public string MachineLearningArguments { get; set; }

        public float? DefaultReward { get; set; } = 0;
    }
}
