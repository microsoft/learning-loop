// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Error;

namespace Microsoft.DecisionService.Common
{
    public class CALearningPolicy : ILearningPolicy
    {
        private readonly string _learningPolicy;

        public CALearningPolicy(string learningPolicy)
        {
            if (!learningPolicy.Contains(MachineLearningArgsHelper.CAPolicyParameter))
            {
                throw new PersonalizerException(PersonalizerErrorCode.InvalidPolicyConfiguration, nameof(learningPolicy) + " is not a valid CA learning policy.");
            }
            _learningPolicy = learningPolicy;
        }

        public string GetLearningPolicy() => _learningPolicy;

        public string GetDefaultArguments() => ApplicationConstants.CADefaultLearningPolicy;

        public string GetInitialCommandLineArguments() => ApplicationConstants.CAInitialCommandLine;

        public int GetProtocolVersion() => ApplicationConstants.V2ProtocolVersion;

        public ProblemType GetProblemType() => ProblemType.CA;
    }
}
