// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.DecisionService.OnlineTrainer;

public class JoinerConfig
{
    public bool JoinerEnabled { get; set; } = false;

    [Required]
    public string AppId { get; set; }

    [Required]
    public DateTime? LastConfigurationEditDate { get; set; } = null;

    public DateTime? WarmstartStartDateTime { get; set; } = null;

    public TimeSpan BackwardEventJoinWindowTimeSpan { get; set; } = TimeSpan.FromMinutes(2);

    public bool UseClientTimestamp { get; set; } = false;

    [Required]
    public TimeSpan? ExperimentalUnitDuration { get; set; } = null;

    /// <summary>
    /// The interaction event hub name
    /// </summary>
    public string InteractionHubName { get; set; } = "interaction";

    [Required]
    public string FullyQualifiedEventHubNamespace { get; set; }

    /// <summary>
    /// The observation event hub name
    /// </summary>
    public string ObservationHubName { get; set; } = "observation";

    public TimeSpan PunctuationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// To avoid clock issues punctuations are send (UtcNow - slack) in joiner.
    /// <see cref="AddPunctuationSlack"/> flag will adjust punctuation to be (last + slack).
    /// </summary>
    public TimeSpan PunctuationSlack { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Flag to adjust from using the fast (UtcNow - slack) in joiner to (last + slack).
    /// </summary>
    public bool AddPunctuationSlack { get; set; } = false;

    /// <summary>
    /// The number of times we will retry when trying to receive an event times out
    /// in the LOJ block.
    /// </summary>
    public int EventReceiveTimeoutMaxRetryCount { get; set; } = 100;

    /// <summary>
    /// Time to wait for events to be ready on a partition before skipping it.
    /// </summary>
    public TimeSpan ActivePartitionReadTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Partitions Source maximum wait time when its event source is not able to read data.
    /// </summary>
    public TimeSpan EventHubReceiveTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The input folder to be used when in local JoinerFiles mode.
    /// </summary>
    /// <remarks>
    /// This is only for JoinerFiles and setting it causes JoinerFiles to be used.
    /// </remarks>
    public string JoinerFilesInputDirectory { get; set; }

    /// <summary>
    /// Size of block buffer used in EventMergeSort
    /// </summary>
    public int EventMergeSortBlockBufferSize { get; set; } = 16;

    /// <summary>
    /// Toggles use of more verbose metrics.
    /// </summary>
    public bool LOJVerboseMetricsEnabled { get; set; } = false;

    /// <summary>
    /// Setting to enable log mirror to another storage account.
    /// </summary>
    public bool LogMirrorEnabled { set; get; }

    /// <summary>
    /// Storage account Sas Uri for log mirroring.
    /// </summary>
    public string LogMirrorSasUri { set; get; }

    public int BlockBufferCapacityForEventBatch { get; set; } = 1;

    public string LocalCookedLogsPath { get; set; }

    public bool IsBillingEnabled { get; set; } = false;
}
