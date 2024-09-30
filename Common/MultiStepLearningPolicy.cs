// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Error;

namespace Microsoft.DecisionService.Common
{
    public class MultiStepLearningPolicy : ILearningPolicy
    {
        private readonly string _learningPolicy;
        private string multiStepArgs = MachineLearningArgsHelper.MultiStepPolicyParameter;

        public MultiStepLearningPolicy(string learningPolicy)
        {
            if (!learningPolicy.Contains(MachineLearningArgsHelper.CBPolicyParameter)
                || !learningPolicy.Contains(multiStepArgs))
            {
                throw new PersonalizerException(PersonalizerErrorCode.InvalidPolicyConfiguration, nameof(learningPolicy) + " is not a valid MultiStep learning policy.");
            }
            // dropping --multistep parameters since RL Client Lib does not use it for inference.
            _learningPolicy = learningPolicy.Replace(multiStepArgs, "").TrimEnd();
        }

        public string GetDefaultArguments() => ApplicationConstants.CBDefaultLearningPolicy;

        public string GetInitialCommandLineArguments() => ApplicationConstants.CBInitialCommandLine;

        public ProblemType GetProblemType() => ProblemType.CB | ProblemType.MULTI_STEP;

        public string GetLearningPolicy() => _learningPolicy;
    }
}
