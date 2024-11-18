// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Error;

namespace Microsoft.DecisionService.Common
{
    public class CCBLearningPolicy : ILearningPolicy
    {
        private readonly string _learningPolicy;

        public CCBLearningPolicy(string learningPolicy)
        {
            if (!learningPolicy.Contains(MachineLearningArgsHelper.CCBPolicyParameter))
            {
                throw new PersonalizerException(PersonalizerErrorCode.InvalidPolicyConfiguration, nameof(learningPolicy) + " is not a valid CCB learning policy.");
            }
            _learningPolicy = learningPolicy;
        }

        public string GetLearningPolicy() => _learningPolicy;

        public string GetDefaultArguments() => ApplicationConstants.CCBDefaultLearningPolicy;

        public string GetInitialCommandLineArguments() => ApplicationConstants.CCBInitialCommandLine;

        public ProblemType GetProblemType() => ProblemType.CCB;
    }
}
