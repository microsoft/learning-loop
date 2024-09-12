// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common
{
    public interface ILearningPolicy
    {
        string GetLearningPolicy();
        string GetDefaultArguments();
        string GetInitialCommandLineArguments();
        ProblemType GetProblemType();
        bool PolicyEquals(ILearningPolicy policy)
        {
            return policy.GetLearningPolicy() == GetLearningPolicy() && policy.GetProblemType() == GetProblemType();
        }
    }
}
