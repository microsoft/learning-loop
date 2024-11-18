// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Trainer;
using Microsoft.DecisionService.Common.Utils.FileSystem;
using Microsoft.DecisionService.VowpalWabbit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Common.Trainer
{
    [TestClass]
    [DoNotParallelize]
    public class LearnMetricsParserTests
    {
        private const string CB_ML_ARGS =
            "--cb_explore_adf --epsilon 0.2 --power_t 0 -l 1e-05 --cb_type ips -q OE -q GS -q OS";

        private const string CCB_ML_ARGS = "--ccb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type mtr -q ::";

        private const string CA_ML_ARGS =
            "--cats 128 --min_value 185 --max_value 23959 --bandwidth 1 --epsilon 0.2 -l 0.5 --l1 1E-07 --power_t 0.5 --coin --loss_option 1";

        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [DataRow(ProblemType.CB)]
        [DataRow(ProblemType.CCB)]
        public void Parse_NullPathThrows(ProblemType type)
        {
            ILearnMetricsParser parser =  CreateParser(type);
            Assert.ThrowsException<ArgumentNullException>(() => parser.Parse(null));
        }

        [DataTestMethod]
        [DataRow(ProblemType.CB)]
        [DataRow(ProblemType.CCB)]
        public void Parse_EmptyFileDefaultResult(ProblemType type)
        {
            ILearnMetricsParser parser =  CreateParser(type);
            using var temp = new TempFile();
            ILearnMetrics metrics = parser.Parse(temp.FilePath);
            LearnMetricsTestUtils.VerifyLearnMetrics(metrics);
        }

        [DataTestMethod]
        [DataRow(ProblemType.CB)]
        [DataRow(ProblemType.CCB)]
        public void Parse_InvalidFileThrows(ProblemType type)
        {
            ILearnMetricsParser parser =  CreateParser(type);
            const string filePath = "foo.txt";
            using (FileStream fs = File.Create(filePath))
            {
                fs.Write(Encoding.UTF8.GetBytes("{"));
            }

            Assert.ThrowsException<Newtonsoft.Json.JsonSerializationException>(() => parser.Parse(filePath));
        }

        [DataTestMethod]
        [DataRow(ProblemType.CB)]
        [DataRow(ProblemType.CCB)]
        public void Parse_MetricsFile(ProblemType type)
        {
            ILearnMetricsParser parser =  CreateParser(type);
            string filePath = GetTestMetricFilePath(type);
            ILearnMetrics metrics = parser.Parse(filePath);
          
            LearnMetricsTestUtils.VerifyLearnMetrics(
                metrics,
                numberOfEvents: 24,
                numberOfLearnedEvents: 23,
                numberOfEventsWithObservation: 23,
                numberOfLearnedEventsWithBaselineActionChosen: 21,
                numberOfLearnedEventsWithBaselineActionNotChosen: 2,
                sumOfLearnedRewards: 36,
                sumOfLearnedRewardsWithBaselineActionChosen: 18,
                firstEventId: "0000000",
                lastEventId:  "0000009",
                firstEventTime: new DateTime(2021, 2, 4, 16, 31, 29, 246),
                lastEventTime: new DateTime(2021, 2, 4, 16, 31, 47, 319),
                averageFeaturesPerEvent: 1156,
                averageFeaturesPerExample: 96,
                averageNamespacesPerEvent: 73,
                averageNamespacesPerExample: 6,
                averageActionsPerEvent: 12);
        }

        [DataTestMethod]
        [DataRow(ProblemType.CB, CB_ML_ARGS)]
        [DataRow(ProblemType.CCB, CCB_ML_ARGS)]
        public async Task Integration_WithVwExeTrainer_Async(ProblemType type, string mlArgs)
        {
            var trainer = new VwRunner(mlArgs);

            string testFile = GetTestDataFilePath(type);
            var fileReader = new FileReader();
            await using var stream = new MemoryStream();
            await fileReader.ReadAsync(testFile, stream);

            var result = await trainer.LearnAsync(stream.ToArray(), null, JoinedLogFormat.DSJSON);

            LearnMetricsTestUtils.VerifyLearnMetrics(
                result.Metrics,
                numberOfEvents: 10,
                numberOfLearnedEvents: 8,
                numberOfEventsWithObservation: type == ProblemType.CCB ? 13 : 8,
                numberOfLearnedEventsWithBaselineActionChosen: 2,
                numberOfLearnedEventsWithBaselineActionNotChosen: type == ProblemType.CCB ? 8 : 6,
                sumOfLearnedRewards: type == ProblemType.CCB ? 4.8f : 5.0f,
                sumOfLearnedRewardsWithBaselineActionChosen: type == ProblemType.CCB ? 1.5f : 1.0f,
                averageFeaturesPerEvent: type == ProblemType.CCB ? 0 : 33,
                averageFeaturesPerExample: type == ProblemType.CCB ? 0 : 11,
                averageNamespacesPerEvent: type == ProblemType.CCB ? 0 : 18,
                averageNamespacesPerExample: type == ProblemType.CCB ? 0 : 6,
                averageActionsPerEvent: 3,
                firstEventId: "0000000",
                lastEventId: "0000009",
                firstEventTime: new DateTime(2021, 4, 7, 16, 40, 25),
                lastEventTime: new DateTime(2021, 4, 7, 16, 49, 25));
        }

        private static string GeTestFileBasePath() =>
            Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Common", "Trainer", "Data", nameof(LearnMetricsParserTests));

        private static string GetTestDataFilePath(ProblemType type)
        {
            return Path.Join(
                GeTestFileBasePath(),
                $"LearnMetricsTests_{type}_Data.json");
        }

        private static string GetTestMetricFilePath(ProblemType type)
        {
            return Path.Join(
                GeTestFileBasePath(),
                $"LearnMetricsTests_{type}_Metrics.json");
        }

        private static ILearnMetricsParser CreateParser(ProblemType type)
        {
            return LearnMetricsParserFactory.Create(type);
        }
    }
}
