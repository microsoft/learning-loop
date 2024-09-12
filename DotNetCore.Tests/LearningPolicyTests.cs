// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Error;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCore.Tests
{
    [TestClass]
    public class LearningPolicyTests
    {
        [TestMethod]
        public void LearningPolicyFactory_CBPolicy()
        {
            string cbArgs = ApplicationConstants.CBDefaultLearningPolicy;
            ILearningPolicy learningPolicy = LearningPolicyFactory.Create(cbArgs);
            Assert.IsInstanceOfType(learningPolicy, typeof(CBLearningPolicy));
        }

        [TestMethod]
        public void LearningPolicyFactory_CCBPolicy()
        {
            string ccbArgs = ApplicationConstants.CCBDefaultLearningPolicy;
            ILearningPolicy learningPolicy = LearningPolicyFactory.Create(ccbArgs);
            Assert.IsInstanceOfType(learningPolicy, typeof(CCBLearningPolicy));
        }

        [TestMethod]
        public void LearningPolicyFactory_CAPolicy()
        {
            string caArgs = ApplicationConstants.CADefaultLearningPolicy;
            ILearningPolicy learningPolicy = LearningPolicyFactory.Create(caArgs);
            Assert.IsInstanceOfType(learningPolicy, typeof(CALearningPolicy));
        }

        [TestMethod]
        public void LearningPolicyFactory_MultiStepPolicy()
        {
            string multiStepArgs = ApplicationConstants.CBDefaultLearningPolicy + " " + MachineLearningArgsHelper.MultiStepPolicyParameter;
            ILearningPolicy learningPolicy = LearningPolicyFactory.Create(multiStepArgs);
            Assert.IsInstanceOfType(learningPolicy, typeof(MultiStepLearningPolicy));
        }

        [TestMethod]
        public void LearningPolicyFactory_CBLargeActionSpacePolicy()
        {
            string cbLargeActionSpaceArgs = ApplicationConstants.CBDefaultLearningPolicy + " " + MachineLearningArgsHelper.LargeActionSpaceParameter;
            ILearningPolicy learningPolicy = LearningPolicyFactory.Create(cbLargeActionSpaceArgs);
            Assert.IsInstanceOfType(learningPolicy, typeof(CBLargeActionSpaceLearningPolicy));
            Assert.AreEqual(cbLargeActionSpaceArgs, learningPolicy.GetLearningPolicy());
        }

        [TestMethod]
        public void LearningPolicyFactory_CCBLargeActionSpacePolicy()
        {
            string ccbLargeActionSpaceArgs = ApplicationConstants.CCBDefaultLearningPolicy + " " + MachineLearningArgsHelper.LargeActionSpaceParameter;
            ILearningPolicy learningPolicy = LearningPolicyFactory.Create(ccbLargeActionSpaceArgs);
            Assert.IsInstanceOfType(learningPolicy, typeof(CCBLargeActionSpaceLearningPolicy));
            Assert.AreEqual(ccbLargeActionSpaceArgs, learningPolicy.GetLearningPolicy());
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void LearningPolicyFactory_OnInvalidArgument_ThrowsException()
        {
            string invalidArgs = "invalid arguments";
            _ = LearningPolicyFactory.Create(invalidArgs);
        }

        [TestMethod]
        public void CBLearningPolicy()
        {
            string cbArgs = ApplicationConstants.CBDefaultLearningPolicy;
            CBLearningPolicy learningPolicy = new CBLearningPolicy(cbArgs);

            Assert.AreEqual(cbArgs, learningPolicy.GetLearningPolicy());
            Assert.AreEqual(ApplicationConstants.CBDefaultLearningPolicy, learningPolicy.GetDefaultArguments());
            Assert.AreEqual(ApplicationConstants.CBInitialCommandLine, learningPolicy.GetInitialCommandLineArguments());
            Assert.AreEqual(ProblemType.CB, learningPolicy.GetProblemType());
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CBLearningPolicyThrows_ForCCBArguments()
        {
            string ccbArgs = ApplicationConstants.CCBDefaultLearningPolicy;
            _ = new CBLearningPolicy(ccbArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CBLearningPolicyThrows_ForCAArguments()
        {
            string caArgs = ApplicationConstants.CADefaultLearningPolicy;
            _ = new CBLearningPolicy(caArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CBLearningPolicyThrows_ForInvalidArguments()
        {
            string invalidArgs = "invalid arguments";
            _ = new CBLearningPolicy(invalidArgs);
        }

        [TestMethod]
        public void CCBLearningPolicy()
        {
            string ccbArgs = ApplicationConstants.CCBDefaultLearningPolicy;
            CCBLearningPolicy learningPolicy = new CCBLearningPolicy(ccbArgs);

            Assert.AreEqual(ccbArgs, learningPolicy.GetLearningPolicy());
            Assert.AreEqual(ApplicationConstants.CCBDefaultLearningPolicy, learningPolicy.GetDefaultArguments());
            Assert.AreEqual(ApplicationConstants.CCBInitialCommandLine, learningPolicy.GetInitialCommandLineArguments());
            Assert.AreEqual(ProblemType.CCB, learningPolicy.GetProblemType());
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CCBLearningPolicyThrows_ForCBArguments()
        {
            string cbArgs = ApplicationConstants.CBDefaultLearningPolicy;
            _ = new CCBLearningPolicy(cbArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CCBLearningPolicyThrows_ForCAArguments()
        {
            string caArgs = ApplicationConstants.CADefaultLearningPolicy;
            _ = new CCBLearningPolicy(caArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CCBLearningPolicyThrows_ForInvalidArguments()
        {
            string invalidArgs = "invalid arguments";
            _ = new CCBLearningPolicy(invalidArgs);
        }

        [TestMethod]
        public void CALearningPolicy()
        {
            string caArgs = ApplicationConstants.CADefaultLearningPolicy;
            CALearningPolicy learningPolicy = new CALearningPolicy(caArgs);

            Assert.AreEqual(caArgs, learningPolicy.GetLearningPolicy());
            Assert.AreEqual(ApplicationConstants.CADefaultLearningPolicy, learningPolicy.GetDefaultArguments());
            Assert.AreEqual(ApplicationConstants.CAInitialCommandLine, learningPolicy.GetInitialCommandLineArguments());
            Assert.AreEqual(ApplicationConstants.V2ProtocolVersion, learningPolicy.GetProtocolVersion());
            Assert.AreEqual(ProblemType.CA, learningPolicy.GetProblemType());
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CALearningPolicyThrows_ForCBArguments()
        {
            string cbArgs = ApplicationConstants.CBDefaultLearningPolicy;
            _ = new CALearningPolicy(cbArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CALearningPolicyThrows_ForCCBArguments()
        {
            string ccbArgs = ApplicationConstants.CCBDefaultLearningPolicy;
            _ = new CALearningPolicy(ccbArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CALearningPolicyThrows_ForInvalidArguments()
        {
            string invalidArgs = "invalid arguments";
            _ = new CALearningPolicy(invalidArgs);
        }

        [TestMethod]
        public void MultiStepLearningPolicy()
        {
            string multiStepArgs = ApplicationConstants.CBDefaultLearningPolicy + " " + MachineLearningArgsHelper.MultiStepPolicyParameter;
            MultiStepLearningPolicy learningPolicy = new MultiStepLearningPolicy(multiStepArgs);

            Assert.AreEqual(ApplicationConstants.CBDefaultLearningPolicy, learningPolicy.GetLearningPolicy());
            Assert.AreEqual(ApplicationConstants.CBDefaultLearningPolicy, learningPolicy.GetDefaultArguments());
            Assert.AreEqual(ApplicationConstants.CBInitialCommandLine, learningPolicy.GetInitialCommandLineArguments());
            Assert.AreEqual(ProblemType.CB | ProblemType.MULTI_STEP, learningPolicy.GetProblemType());
            // Make sure adding MULTI_STEP ProblemTyle does not break exisiting logic.
            Assert.AreNotEqual(ProblemType.CB, learningPolicy.GetProblemType());
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void MultiStepLearningPolicyThrows_ForCBArguments()
        {
            string cbArgs = ApplicationConstants.CBDefaultLearningPolicy;
            _ = new MultiStepLearningPolicy(cbArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void MultiStepLearningPolicyThrows_ForCCBArguments()
        {
            string ccbArgs = ApplicationConstants.CCBDefaultLearningPolicy;
            _ = new MultiStepLearningPolicy(ccbArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CBLargeActionSpaceLearningPolicyThrows_ForCCBArguments()
        {
            string ccbArgs = ApplicationConstants.CCBLargeActionSpaceInitialCommandLine;
            _ = new CBLargeActionSpaceLearningPolicy(ccbArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CBLargeActionSpaceLearningPolicyThrows_ForMissingCBParameter()
        {
            string caArgs = ApplicationConstants.CADefaultLearningPolicy + " --large_action_space";
            _ = new CBLargeActionSpaceLearningPolicy(caArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CBLargeActionSpaceLearningPolicyThrows_ForMissingLargeActionSpaceParameter()
        {
            string cbArgs = ApplicationConstants.CBDefaultLearningPolicy;
            _ = new CBLargeActionSpaceLearningPolicy(cbArgs);
        }

        [DataTestMethod]
        [DataRow("-2")]
        [DataRow("0")]
        [DataRow("invalid")]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CBLargeActionSpaceLearningPolicyThrows_ForInvalidMaxActionsParameterValue(string maxActions)
        {
            string cbArgs = ApplicationConstants.CBLargeActionSpaceInitialCommandLine + $" --max_actions {maxActions}";
            _ = new CBLargeActionSpaceLearningPolicy(cbArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CCBLargeActionSpaceLearningPolicyThrows_ForMissingCCBParameter()
        {
            string cbArgs = ApplicationConstants.CBLargeActionSpaceInitialCommandLine;
            _ = new CCBLargeActionSpaceLearningPolicy(cbArgs);
        }

        [TestMethod]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CCBLargeActionSpaceLearningPolicyThrows_ForMissingLargeActionSpaceParameter()
        {
            string ccbArgs = ApplicationConstants.CCBDefaultLearningPolicy;
            _ = new CCBLargeActionSpaceLearningPolicy(ccbArgs);
        }

        [DataTestMethod]
        [DataRow("-2")]
        [DataRow("0")]
        [DataRow("invalid")]
        [ExpectedException(typeof(PersonalizerException), noExceptionMessage: "Expected exception if arguments are not valid")]
        public void CCBLargeActionSpaceLearningPolicyThrows_ForNegativeMaxActionsParameterValue(string maxActions)
        {
            string ccbArgs = ApplicationConstants.CCBLargeActionSpaceInitialCommandLine + $" --max_actions {maxActions}";
            _ = new CCBLargeActionSpaceLearningPolicy(ccbArgs);
        }
    }
}