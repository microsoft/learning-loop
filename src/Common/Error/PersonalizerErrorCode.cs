// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Net;

namespace Microsoft.DecisionService.Common.Error
{
    ///<summary>Error Codes returned by Personalizer</summary>
    public enum PersonalizerErrorCode
    {
        // 400 Errors

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.BadRequest)]
        BadRequest,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidServiceConfiguration)]
        InvalidServiceConfiguration,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidLearningModeServiceConfiguration)]
        InvalidLearningModeServiceConfiguration,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidPolicyConfiguration)]
        InvalidPolicyConfiguration,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidPolicyContract)]
        InvalidPolicyContract,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidEvaluationContract)]
        InvalidEvaluationContract,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidDateTimesInEvaluationContract)]
        InvalidDateTimesInEvaluationContract,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidContract)]
        InvalidContract,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.DuplicateCustomPolicyNames)]
        DuplicateCustomPolicyNames,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.MaximumEvaluationCustomPoliciesExceeded)]
        MaximumEvaluationCustomPoliciesExceeded,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.EvaluationNoLogsExistInDateRange)]
        NoLogsExistInDateRange,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.EvaluationLogsSizeExceedAllowedLimit)]
        LogsSizeExceedAllowedLimit,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidRewardRequest)]
        InvalidRewardRequest,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidEventIdToActivate)]
        InvalidEventIdToActivate,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidRequest)]
        InvalidRankRequest,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidRequest)]
        InvalidExportLogsRequest,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidRequest)]
        InvalidRequest,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidSasUri)]
        InvalidSasUri,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidModelMetadata)]
        InvalidModelMetadata,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.ApprenticeModeNeverTurnedOn)]
        ApprenticeModeNeverTurnedOn,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.MissingAppId)]
        MissingAppId,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidSingleTenantEventHubRewardWaitTime)]
        InvalidSingleTenantEventHubRewardWaitTime,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidLogRetentionDays)]
        InvalidLogRetentionDays,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidMultiSlotApiAccess)]
        InvalidMultiSlotApiAccess,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidEpisode)]
        InvalidEpisode,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidMultiStepApiAccess)]
        InvalidMultiStepApiAccess,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.ExceedsMaximumAllowedPayloadSize)]
        PayloadSizeExceeded,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidModelImportSignature)]
        InvalidModelImportSignature,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidModelImportFormat)]
        InvalidModelImportFormat,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.FeatureImportanceAlreadyExists)]
        FeatureImportanceAlreadyExists,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.EvaluationAlreadyExists)]
        EvaluationAlreadyExists,

        [HttpStatusCode(HttpStatusCode.BadRequest)]
        [Description(PersonalizerErrorMessages.InvalidEpisodeDepth)]
        InvalidEpisodeDepth,
        // 403 errors

        [HttpStatusCode(HttpStatusCode.Forbidden)]
        [Description(PersonalizerErrorMessages.InvalidApiAccess)]
        InvalidApiAccess,

        [HttpStatusCode(HttpStatusCode.Forbidden)]
        [Description(PersonalizerErrorMessages.ModelFileAccessDenied)]
        ModelFileAccessDenied,

        [HttpStatusCode(HttpStatusCode.Forbidden)]
        [Description(PersonalizerErrorMessages.ProblemTypeIncompatibleWithAutoOptimization)]
        ProblemTypeIncompatibleWithAutoOptimization,

        // 404 errors

        [HttpStatusCode(HttpStatusCode.NotFound)]
        [Description(PersonalizerErrorMessages.ResourceNotFound)]
        ResourceNotFound,

        [HttpStatusCode(HttpStatusCode.NotFound)]
        [Description(PersonalizerErrorMessages.FrontEndNotFound)]
        FrontEndNotFound,

        [HttpStatusCode(HttpStatusCode.NotFound)]
        [Description(PersonalizerErrorMessages.EvaluationNotFound)]
        EvaluationNotFound,

        [HttpStatusCode(HttpStatusCode.NotFound)]
        [Description(PersonalizerErrorMessages.FeatureImportanceNotFound)]
        FeatureImportanceNotFound,

        [HttpStatusCode(HttpStatusCode.NotFound)]
        [Description(PersonalizerErrorMessages.LearningSettingsNotFound)]
        LearningSettingsNotFound,

        [HttpStatusCode(HttpStatusCode.NotFound)]
        [Description(PersonalizerErrorMessages.EvaluationModelNotFound)]
        EvaluationModelNotFound,

        [HttpStatusCode(HttpStatusCode.NotFound)]
        [Description(PersonalizerErrorMessages.LogsPropertiesNotFound)]
        LogsPropertiesNotFound,

        //405 errors
        [HttpStatusCode(HttpStatusCode.MethodNotAllowed)]
        [Description(PersonalizerErrorMessages.IncompatibleEvaluationVersion)]
        IncompatibleEvaluationVersion,

        // 408 errors

        [HttpStatusCode(HttpStatusCode.RequestTimeout)]
        [Description(PersonalizerErrorMessages.TaskCanceled)]
        RequestTimeout,

        // 409 error

        [HttpStatusCode(HttpStatusCode.Conflict)]
        [Description(PersonalizerErrorMessages.ActionRankingError)]
        ModelRankingError,

        // 500 errors

        [HttpStatusCode(HttpStatusCode.InternalServerError)]
        [Description(PersonalizerErrorMessages.InternalServerError)]
        InternalServerError,

        [HttpStatusCode(HttpStatusCode.InternalServerError)]
        [Description(PersonalizerErrorMessages.ModelDownloadFailed)]
        ModelDownloadFailed,

        [HttpStatusCode(HttpStatusCode.InternalServerError)]
        [Description(PersonalizerErrorMessages.RankNullResponse)]
        RankNullResponse,

        [HttpStatusCode(HttpStatusCode.InternalServerError)]
        [Description(PersonalizerErrorMessages.UpdateConfigurationFailed)]
        UpdateConfigurationFailed,

        [HttpStatusCode(HttpStatusCode.InternalServerError)]
        [Description(PersonalizerErrorMessages.InferenceModelUpdateFailed)]
        InferenceModelUpdateFailed,

        [HttpStatusCode(HttpStatusCode.InternalServerError)]
        [Description(PersonalizerErrorMessages.ModelResetFailed)]
        ModelResetFailed,

        [HttpStatusCode(HttpStatusCode.InternalServerError)]
        [Description(PersonalizerErrorMessages.ModelPublishFailed)]
        ModelPublishFailed,

        [HttpStatusCode(HttpStatusCode.InternalServerError)]
        [Description(PersonalizerErrorMessages.ModelMetadataUpdateFailed)]
        ModelMetadataUpdateFailed,

        [HttpStatusCode(HttpStatusCode.InternalServerError)]
        [Description(PersonalizerErrorMessages.EvaluationsGetListFailed)]
        EvaluationsGetListFailed,

        // 503 errors

        [HttpStatusCode(HttpStatusCode.ServiceUnavailable)]
        [Description(PersonalizerErrorMessages.OperationNotAllowed)]
        OperationNotAllowed,

        None,
    }
}