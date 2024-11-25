// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.VowpalWabbit;

namespace Microsoft.DecisionService.OnlineTrainer;

public class TrainerConfig
{
    private double _explorationPercentage = -1f;
    private string _machineLearningArguments;

    [Required] public string AppId { get; set; }

    [Required]
    [CustomValidation(typeof(VwOptionValidator), "Validate")]
    public string MachineLearningArguments
    {
        get => _machineLearningArguments;
        set => _machineLearningArguments =
            MachineLearningArgsHelper.UpdateExplorationPercentage(value, ExplorationPercentage);
    }

    // TODO: ensure that this is only set via this param and not in the ml args param
    /// <summary>
    /// Sets the epsilon value field in the MachineLearningArguments
    /// </summary>
    public double ExplorationPercentage
    {
        set
        {
            _explorationPercentage = value;
            MachineLearningArguments = _machineLearningArguments =
                MachineLearningArgsHelper.UpdateExplorationPercentage(_machineLearningArguments, value);
        }
        get => _explorationPercentage;
    }

    public Uri? WarmstartModelUrl { get; set; } = null;

    public DateTime? WarmstartStartDateTime { get; set; } = null;

    public float? DefaultReward { get; set; } = 0;

    public TimeSpan ModelCheckpointFrequency { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan ModelExportFrequency { get; set; } = TimeSpan.FromMinutes(1);

    [Required] public DateTime? LastConfigurationEditDate { get; set; } = null;

    public bool TrainerEnabled { get; set; } = false;

    /// <summary>
    /// Local path to use for cooked log storage.
    /// </summary>
    public string? LocalCookedLogsPath { get; set; } = null;

    /// <summary>
    /// NOTE:  This is setting used for tests only.  Should not be used in normal operation
    /// Allow Checkpoint and Export frequency below System specified minimums.
    /// </summary>
    public bool IgnoreCheckpointAndExportFrequencyCapping { get; set; } = false;

    /// <summary>
    /// When auto publish is enabled (= default), the trainer exports the model and override the "current" model.
    /// When auto publish is disabled, the trainer exports models but does not override the "current" model (this is done by the user through API calls).
    /// </summary>
    public bool ModelAutoPublish { get; set; } = true;

    /// <summary>
    /// Length of staged models history in days.
    /// Old staged models are automatically deleted from history.
    /// </summary>
    public int StagedModelHistoryLength { get; set; } = 10;

    /// <summary>
    /// Size of block buffer used to process a batch of events, resulting in a block in storage.
    /// Be careful of this size, each may mean 100MB, so this number should be small, like 1 or 2
    /// </summary>
    public int BlockBufferCapacityForEventBatch { get; set; } = 1;

    public ProblemType ProblemType => MachineLearningArgsHelper.ExtractProblemType(MachineLearningArguments);

    /// <summary>
    /// The VW binary to use for training. If null, this tries to select a bundled VW binary.
    /// </summary>
    public string? VwBinaryPath { get; set; } = null;
}