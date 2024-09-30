// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Error;
using System;

namespace Microsoft.DecisionService.Common
{
    public static class LearningPolicyFactory
    {
        private static readonly Func<string, bool> CBLargeActionSpacePolicyEvaluator = (mlArgs =>
        {
            return mlArgs.Contains(MachineLearningArgsHelper.CBPolicyParameter) &&
                   mlArgs.Contains(MachineLearningArgsHelper.LargeActionSpaceParameter) &&
                   !mlArgs.Contains(MachineLearningArgsHelper.MultiStepPolicyParameter);
        });

        private static readonly Func<string, bool> CCBLargeActionSpacePolicyEvaluator = (mlArgs =>
        {
            return mlArgs.Contains(MachineLearningArgsHelper.CCBPolicyParameter) &&
                   mlArgs.Contains(MachineLearningArgsHelper.LargeActionSpaceParameter) &&
                   !mlArgs.Contains(MachineLearningArgsHelper.MultiStepPolicyParameter);
        });

        private static readonly Func<string, bool> CBPolicyEvaluator = (mlArgs =>
        {
            return mlArgs.Contains(MachineLearningArgsHelper.CBPolicyParameter) && 
                   !mlArgs.Contains(MachineLearningArgsHelper.MultiStepPolicyParameter) &&
                   !mlArgs.Contains(MachineLearningArgsHelper.LargeActionSpaceParameter);
        });

        private static readonly Func<string, bool> CCBPolicyEvaluator = (mlArgs =>
        {
            return mlArgs.Contains(MachineLearningArgsHelper.CCBPolicyParameter) && 
                   !mlArgs.Contains(MachineLearningArgsHelper.MultiStepPolicyParameter) &&
                   !mlArgs.Contains(MachineLearningArgsHelper.LargeActionSpaceParameter);
        });

        private static readonly Func<string, bool> CAPolicyEvaluator = (mlArgs =>
        {
            return mlArgs.Contains(MachineLearningArgsHelper.CAPolicyParameter) && 
                   !mlArgs.Contains(MachineLearningArgsHelper.MultiStepPolicyParameter) &&
                   !mlArgs.Contains(MachineLearningArgsHelper.LargeActionSpaceParameter);
        });

        private static readonly Func<string, bool> MultiStepPolicyEvaluator = (mlArgs =>
        {
            return mlArgs.Contains(MachineLearningArgsHelper.CBPolicyParameter) && 
                   mlArgs.Contains(MachineLearningArgsHelper.MultiStepPolicyParameter) &&
                   !mlArgs.Contains(MachineLearningArgsHelper.LargeActionSpaceParameter);
        });

        public static ILearningPolicy Create(string mlArgs)
        {
            if (CCBLargeActionSpacePolicyEvaluator(mlArgs))
                return new CCBLargeActionSpaceLearningPolicy(mlArgs);
            else if (CBLargeActionSpacePolicyEvaluator(mlArgs))
                return new CBLargeActionSpaceLearningPolicy(mlArgs);
            else if (CCBPolicyEvaluator(mlArgs))
                return new CCBLearningPolicy(mlArgs);
            else if (CBPolicyEvaluator(mlArgs))
                return new CBLearningPolicy(mlArgs);
            else if (CAPolicyEvaluator(mlArgs))
                return new CALearningPolicy(mlArgs);
            else if (MultiStepPolicyEvaluator(mlArgs))
            {
                return new MultiStepLearningPolicy(mlArgs);
            }
            else
            {
                throw new PersonalizerException(PersonalizerErrorCode.InvalidPolicyConfiguration);
            }
        }
    }
}
