// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Common.Trainer
{
    [TestClass]
    public class LearnMetricsParserFactoryTests
    {
        [TestMethod]
        public void Create_JoinedLogFormat_DSJson_CB()
        {
            ILearnMetricsParser parser = LearnMetricsParserFactory.Create(ProblemType.CB);
            Assert.IsInstanceOfType(parser, typeof(ILearnMetricsParser));
            Assert.IsInstanceOfType(parser, typeof(VwLearnMetricsFileParser<VwCbLearnMetrics>));
        }

        [TestMethod]
        public void Create_JoinedLogFormat_DSJson_CCB()
        {
            var parser = LearnMetricsParserFactory.Create(ProblemType.CCB);
            Assert.IsInstanceOfType(parser, typeof(ILearnMetricsParser));
            Assert.IsInstanceOfType(parser, typeof(VwLearnMetricsFileParser<VwCcbLearnMetrics>));
        }

        [TestMethod]
        public void Create_JoinedLogFormat_DSJson_CA_Throws()
        {
            Assert.ThrowsException<NotImplementedException>(
                ()=> LearnMetricsParserFactory.Create(ProblemType.CA));
        }
    }
}
