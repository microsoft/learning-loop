// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Trainer;

/// <summary>
/// <see cref="IOnlineTrainer"/> implementation that uses VW exe to train a model.
/// </summary>
public interface IOnlineTrainer
{
    public class LearnResult
    {
        public ILearnMetrics Metrics { get; set; }
        public byte[] FinalModel { get; set; }
    }

    public Task<LearnResult> LearnAsync(
        byte[] joinedLogData,
        byte[]? inputModel,
        JoinedLogFormat format,
        CancellationToken cancellationToken = default);

    public Task<byte[]> ConvertToInferenceModelAsync(
        byte[] inputModel,
        string modelId,
        CancellationToken cancellationToken = default);
    
    public Task<byte[]> ExportTrainerModelAsync(
        byte[] inputModel,
        string modelId,
        CancellationToken cancellationToken = default);
    
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string>? Errors { get; set; }
    }

    public Task<ValidationResult> ValidateModelAsync(byte[] modelData,
        CancellationToken cancellationToken = default);
}