// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.RegularExpressions;

namespace Microsoft.DecisionService.Common
{
    public class MachineLearningArgsHelper
    {
        private const string extractExplorationPercentagePattern = @"--epsilon\s+(\S+)";
        private const string extractMaxActionsPattern = @"--max_actions\s+(\S+)";
        public const string CBPolicyParameter = "--cb_explore_adf";
        public const string CCBPolicyParameter = "--ccb_explore_adf";
        public const string CAPolicyParameter = "--cats";
        public const string MultiStepPolicyParameter = "--multistep";
        public const string LargeActionSpaceParameter = "--large_action_space";

        public static int? ExtractMaxActions(string mlArgs)
        {
            var match = Regex.Match(mlArgs, extractMaxActionsPattern);
            if (match.Success)
            {
                var cap = match.Groups[1].Value;
                if (int.TryParse(cap, out int maxActions))
                {
                    return maxActions;
                }

                return -1;
            }

            return null;
        }

        public static double? ExtractExplorationPercentage(string mlArgs)
        {
            var match = Regex.Match(mlArgs, extractExplorationPercentagePattern);
            if (match.Success)
            {
                var cap = match.Groups[1].Value;
                return Convert.ToDouble(cap);
            }
            return null;
        }

        public static ProblemType ExtractProblemType(string mlArgs)
        {
            if (mlArgs?.Contains("slates") ?? false)
            {
                return ProblemType.SLATES;
            }
            if (mlArgs?.Contains("ccb_explore_adf") ?? false)
            {
                return ProblemType.CCB;
            }
            if (mlArgs?.Contains("cb_explore_adf") ?? false)
            {
                return ProblemType.CB;
            }
            if (mlArgs?.Contains("cats") ?? false)
            {
                return ProblemType.CA;
            }
            return ProblemType.UNKNOWN;
        }

        public static string UpdateExplorationPercentage(string mlArgs, double explorationPercentage)
        {
            if (explorationPercentage >= 0 && explorationPercentage <= 1 && !String.IsNullOrEmpty(mlArgs))
            {
                Regex regex = new Regex(extractExplorationPercentagePattern);
                string newargs = regex.Replace(mlArgs, $"--epsilon {explorationPercentage}");

                return newargs;
            }
            return mlArgs;
        }

        /// <summary>
        /// Remove all model input and output arguments.
        /// </summary>
        /// <param name="mlArgs">VW command line arguments.</param>
        /// <returns>VW command line arguments without model input and output arguments.</returns>
        public static string RemoveModelAndDataArguments(string mlArgs)
        {
            if (mlArgs == null)
            {
                return null;
            }

            if (String.IsNullOrWhiteSpace(mlArgs))
            {
                return string.Empty;
            }

            return Regex.Replace(mlArgs, @"(^|\s+)(-d|--data|-f|--final_regressor|-i|--initial_regressor|--id)\s+\S*", string.Empty).Trim();
        }
    }
}
