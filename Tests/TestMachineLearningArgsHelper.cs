// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonTest
{
    [TestClass]
    public class TestMachineLearningArgsHelper
    {
        private const string testPolicyWithEpsilon = "--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type mtr -q ::";
        private const string testPolicyNoEpsilon = "--cb_explore_adf --power_t 0 -l 0.001 --cb_type mtr -q ::";
        private const string testPolicyNegativeEpsilon = "--cb_explore_adf --epsilon -0.2 --power_t 0 -l 0.001 --cb_type mtr -q ::";

        [DataTestMethod]
        [DataRow(testPolicyWithEpsilon, 0.2)]
        [DataRow(testPolicyNoEpsilon, null)]
        [DataRow(testPolicyNegativeEpsilon, -0.2)]
        public void ExtractExplorationPercentageTest(string mlArgs, double? expectedExplorationPercentage)
        {
            Assert.AreEqual(MachineLearningArgsHelper.ExtractExplorationPercentage(mlArgs), expectedExplorationPercentage);
        }

        [TestMethod]
        public void ExtractLargeActionSpaceMaxActions()
        {
            Assert.IsTrue(MachineLearningArgsHelper.ExtractMaxActions("--cb_explore_adf --large_action_space") == null);
            Assert.IsTrue(MachineLearningArgsHelper.ExtractMaxActions("--cb_explore_adf --large_action_space --max_actions 2") == 2);
            Assert.IsTrue(MachineLearningArgsHelper.ExtractMaxActions("--cb_explore_adf --large_action_space --max_actions -2") == -2);
            Assert.IsTrue(MachineLearningArgsHelper.ExtractMaxActions("--cb_explore_adf --large_action_space --max_actions 0") == 0);
            Assert.IsTrue(MachineLearningArgsHelper.ExtractMaxActions("--cb_explore_adf --large_action_space --max_actions invalid") == -1);
        }

        [DataTestMethod]
        [DataRow(testPolicyWithEpsilon, 0.3, "--cb_explore_adf --epsilon 0.3 --power_t 0 -l 0.001 --cb_type mtr -q ::")]
        [DataRow(testPolicyNoEpsilon, 0.3, testPolicyNoEpsilon)]
        [DataRow(testPolicyNegativeEpsilon, 0.3, "--cb_explore_adf --epsilon 0.3 --power_t 0 -l 0.001 --cb_type mtr -q ::")]
        public void UpdateExplorationPercentageTest(string mlArgs, double newExplorationPercentage, string expectedArgs)
        {
            Assert.AreEqual(MachineLearningArgsHelper.UpdateExplorationPercentage(mlArgs, newExplorationPercentage), expectedArgs);
        }
        
        [DataTestMethod]
        [DataRow("--cb_explore_adf --epsilon 0.3 --id abdc-dabsddce/asasbousd-asdfasre -d data_file.json -f output.vw -i input.vw --power_t 0 -l 0.001 --cb_type mtr -q ::", "--cb_explore_adf --epsilon 0.3 --power_t 0 -l 0.001 --cb_type mtr -q ::")]
        [DataRow("--id abdc-dabsddce/asasbousd-asdfasre --cb_explore_adf --epsilon 0.3 -d data_file.json -f output.vw -i input.vw --power_t 0 -l 0.001 --cb_type mtr -q ::", "--cb_explore_adf --epsilon 0.3 --power_t 0 -l 0.001 --cb_type mtr -q ::")]
        [DataRow("-d data_file.json --cb_explore_adf --epsilon 0.3 --id abdc-dabsddce/asasbousd-asdfasre -f output.vw -i input.vw --power_t 0 -l 0.001 --cb_type mtr -q ::", "--cb_explore_adf --epsilon 0.3 --power_t 0 -l 0.001 --cb_type mtr -q ::")]
        [DataRow("-f output.vw --cb_explore_adf --epsilon 0.3 --id abdc-dabsddce/asasbousd-asdfasre -d data_file.json --initial_regressor input.vw --power_t 0 -l 0.001 --cb_type mtr -q ::", "--cb_explore_adf --epsilon 0.3 --power_t 0 -l 0.001 --cb_type mtr -q ::")]
        [DataRow("-i input.vw --cb_explore_adf --epsilon 0.3 --id abdc-dabsddce/asasbousd-asdfasre -d data_file.json --final_regressor output.vw --power_t 0 -l 0.001 --cb_type mtr -q ::  ", "--cb_explore_adf --epsilon 0.3 --power_t 0 -l 0.001 --cb_type mtr -q ::")]
        [DataRow("  --cb_explore_adf --epsilon 0.3 --power_t 0 -l 0.001 --cb_type mtr -q ::", "--cb_explore_adf --epsilon 0.3 --power_t 0 -l 0.001 --cb_type mtr -q ::")]
        [DataRow(" ", "")]
        [DataRow("", "")]
        [DataRow(null, null)]
        public void RemoveModelAndDataArguments(string mlArgs, string expectedArgs)
        {
            Assert.AreEqual(expectedArgs, MachineLearningArgsHelper.RemoveModelAndDataArguments(mlArgs));
        }
    }
}
