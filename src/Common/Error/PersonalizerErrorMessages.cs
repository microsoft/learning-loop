// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Error
{
    public static class PersonalizerErrorMessages
    {
        // 4XX
        // -- BadRequest
        public const string BadRequest = "Request could not be understood by the server.";
        public const string InvalidServiceConfiguration = "Invalid service configuration.";
        public const string InvalidPolicyConfiguration = "Invalid policy configuration.";
        public const string MissingLearningPolicyAlgorithm = "The learning policy must contain --cb_explore_adf or --cats.";
        public const string InvalidLearningModeServiceConfiguration = "Updating defaultReward, rewardWaitTime and rewardAggregation when changing learning mode from Online to Apprentice mode and vice versa is not allowed. Make the mode change and then change the additional settings with an additional API call.";
        public const string InvalidPolicyContract = "Invalid policy contract.";
        public const string InvalidEvaluationContract = "Invalid evaluation contract.";
        public const string InvalidDateTimesInEvaluationContract = "Invalid evaluation contract. StartTime must be later than or equal to EndTime.";
        public const string InvalidContract = "Invalid contract.";
        public const string DuplicateCustomPolicyNames = "Custom policy names should be unique.";
        public const string MaximumEvaluationCustomPoliciesExceeded = "A maximum of 10 custom policies are allowed";
        public const string EvaluationNoLogsExistInDateRange = "No logs exist in date range.";
        public const string EvaluationLogsSizeExceedAllowedLimit = "Total size of logs exceed allowed limit.";
        public const string EvaluationNotCompleted = "The evaluation must be completed.";
        public const string FeatureImportanceAlreadyExists = "Feature importance with id {0} already exists.";
        public const string EvaluationAlreadyExists = "Evaluation with id {0} already exists.";
        public const string InvalidRewardRequest = "Invalid reward request.";
        public const string EmptyEventId = "EventId should be non-empty or null.";
        public const string EmptyActionId = "Action ids must not be empty.";
        public const string ActionCountOutOfBounds = "Action count should be between 1 and 50.";
        public const string ActionHasNoNonNullFeatures = "Every action should have at least one non-null action feature.";
        public const string ActionsContainsNull = "Actions contains null value.";
        public const string ContextFeaturesContainsNull = "Context features contains null value.";
        public const string FeatureContainsNonNumberArray = "Features should not contain array with non-number items.";
        public const string FeatureHasEmptyStringId = "Context features and action features cannot use the empty string as an id.";
        public const string FeaturesNotInKeyValueFormat = "Features are not in key value pair format.";
        public const string InvalidEventIdToActivate = "Invalid activate event request.";
        public const string InvalidModelMetadata = "Invalid model metadata.";
        public const string ApprenticeModeNeverTurnedOn = "Apprentice mode never turned on.";
        public const string ModelResetFailed = "Model reset failed.";
        public const string ModelMetadataUpdateFailed = "Model metadata update failed.";
        public const string InvalidModelImportSignature = "Given model file is not signed or does not have a valid signature.";
        public const string InvalidModelImportFormat = "Given model file format is invalid.";
        public const string ModelPublishFailed = "Model publish failed.";
        public const string InvalidRequest = "Invalid request.";
        public const string InvalidSasUri = "SAS Uri must be the Uri to a container that has write permissions; Or the date in SAS Uri is not in the format of yyyy-mm-ddThh:mm:ssZ.";
        public const string InvalidAutoOptimizationFrequency = "The auto-optimization frequency must be atleast {0} days.";
        public const string ModelFileAccessDenied = "Key vault Key used for customer managed key cannot be accessed.";
        public const string ProblemTypeIncompatibleWithAutoOptimization = "Auto-optimization is not compatible with multi-slot personalization.";
        public const string MissingAppId = "AppId is missing in the header.";
        public const string InvalidSingleTenantEventHubRewardWaitTime = "Reward wait time should be two days or less.";
        public const string InvalidLogRetentionDays = "Log Retention Days must be -1 to store indefinitely or must be at least reward wait time plus 1 day (rounded up)";
        public const string ActionRankingError = "Error while ranking actions using model. Please verify the learning settings are valid.";
        public const string MultiSlotNoSlotsDefined = "At lease one slot must be defined in a multi-slot rank request.";
        public const string SlotContainsNull = "Slots contain null value.";
        public const string MultiSlotsCountOutOfBounds = "Slots count should be between 1 and the number of actions";
        public const string MultiSlotInvalidExcludedActions = "All excluded action ID's must have corresponding action ID";
        public const string InvalidBaselineActionId = "Each slot must contain a unique BaselineAction with an ID corresponding to an element of Actions.";
        public const string ExcludedBaselineAction = "BaselineAction cannot be part of ExcludedActions.";
        public const string MultiSlotInvalidSlotIds = "All slot's must contain unique, non empty ID";
        public const string InvalidMultiSlotApiAccess = "Multi-slot feature is currently disabled. Please follow multi-slot Personalizer documentation to update your instance settings to enable multi-slot functionality.";
        public const string InvalidEpisode = "Invalid�episode�information.�The provided previousEventId�has no history within this�episode.";
        public const string InvalidMultiStepApiAccess = "Multi-step feature is currently disabled. Please follow multi-step Personalizer documentation to update your instance settings to enable multi-step functionality.";
        public const string InvalidEpisodeDepth = "The allowed episode depth is between 0 and {0}.";
        public const string InvalidApiAccess = "Api is currently disabled for the instance.";
        public const string ExceedsMaximumAllowedPayloadSize = "Exceeds maximum allowed payload size.";
        public const string IncompatibleIntervalAndWindow = "Incompatible intervalInMinutes and window parameters. Valid rolling window intervals: 60 minutes, 360 minutes, 720 minutes, 1440 minutes. Valid expanding window interval: 5 minutes.";
        public const string IncompatibleEvaluationVersion = "GetEvaluation cannot be called with an API version newer than the version used to create the evaluation.";
        public const string FilterEvaluationsQuery = "Error in the filter expression. Use an expression to filter the evaluations against evaluation metadata. Only evaluations where the expression evaluates to true are included in the response. Here is an example, metadata=evaluationType eq 'Manual'.";
        public const string InvalidModelRetrainDays = "Model retrain days must be between 0 and the number of log retention days.";



        // -- ResourceNotFound
        public const string ResourceNotFound = "Requested resource does not exist on the server.";
        public const string FrontEndNotFound = "Front end not found.";
        public const string LiveModelNotFound = "Live model not found.";
        public const string EvaluationNotFound = "Offline Evaluation not found.";
        public const string FeatureImportanceNotFound = "Feature importance not found.";
        public const string LearningSettingsNotFound = "Learning Settings not found in evaluation.";
        public const string EvaluationModelNotFound = "Model not found in evaluation.";
        public const string LogsPropertiesNotFound = "Log properties not found.";

        // -- RequestTimeout
        public const string TaskCanceled = "The task was canceled.";

        // 5XX
        // -- InternalServerError
        public const string InferenceModelUpdateFailed = "Failed to update inference model.";
        public const string InternalServerError = "A generic error has occurred on the server.";
        public const string ModelDownloadFailed = "Failed to download model.";
        public const string RankNullResponse = "Rank call returned null response.";
        public const string UpdateConfigurationFailed = "Failed to update configuration.";
        public const string UploadVWFileFailed = "Failed to upload vw file to Azure storage account";
        public const string OperationNotAllowed = "This operation is not allowed at this time.";
        public const string EvaluationsGetListFailed = "Failed to get evaluations list.";
    }
}