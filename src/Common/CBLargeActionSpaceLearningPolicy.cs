// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Error;

namespace Microsoft.DecisionService.Common
{
    public class CBLargeActionSpaceLearningPolicy : ILearningPolicy
    {
        private readonly string _learningPolicy;
        private readonly int _maxActions = -1;

        public CBLargeActionSpaceLearningPolicy(string learningPolicy)
        {
            var maxActions = MachineLearningArgsHelper.ExtractMaxActions(learningPolicy);

            if (learningPolicy.Contains(MachineLearningArgsHelper.CCBPolicyParameter) ||
                !learningPolicy.Contains(MachineLearningArgsHelper.CBPolicyParameter) ||
                !learningPolicy.Contains(MachineLearningArgsHelper.LargeActionSpaceParameter) ||
                (maxActions != null && maxActions.Value <= 0))
            {
                throw new PersonalizerException(PersonalizerErrorCode.InvalidPolicyConfiguration, nameof(learningPolicy) + " is not a valid CB Large Action Space learning policy.");
            }
            _learningPolicy = learningPolicy;
            _maxActions = maxActions != null ? maxActions.Value : _maxActions;
        }

        public string GetLearningPolicy() => _learningPolicy;

        public string GetDefaultArguments() => ApplicationConstants.CBDefaultLearningPolicy;

        public string GetInitialCommandLineArguments()
        {
            if (_maxActions > 0)
            {
                return $"{ApplicationConstants.CBLargeActionSpaceInitialCommandLine} --max_actions {_maxActions}";
            }

            return ApplicationConstants.CBLargeActionSpaceInitialCommandLine;
        }

        public ProblemType GetProblemType() => ProblemType.CB | ProblemType.LARGE_ACTION_SPACE;
    }
}

