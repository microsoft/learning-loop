// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Error;

namespace Microsoft.DecisionService.Common
{
    public class CBLearningPolicy : ILearningPolicy
    {
        private readonly string _learningPolicy;

        public CBLearningPolicy(string learningPolicy)
        {
            if (learningPolicy.Contains(MachineLearningArgsHelper.CCBPolicyParameter) || !learningPolicy.Contains(MachineLearningArgsHelper.CBPolicyParameter))
            {
                throw new PersonalizerException(PersonalizerErrorCode.InvalidPolicyConfiguration, nameof(learningPolicy) + " is not a valid CB learning policy.");
            }
            _learningPolicy = learningPolicy;
        }

        public string GetLearningPolicy() => _learningPolicy;

        public string GetDefaultArguments() => ApplicationConstants.CBDefaultLearningPolicy;

        public string GetInitialCommandLineArguments() => ApplicationConstants.CBInitialCommandLine;

        public ProblemType GetProblemType() => ProblemType.CB;
    }
}
