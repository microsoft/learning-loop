// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest;
using Microsoft.DecisionService.Common;
using Microsoft.DecisionService.Common.Utils.Exe;
using Microsoft.DecisionService.VowpalWabbit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VW.VWBinResolver;
using System.IO;
using System.Threading.Tasks;

namespace Tests.OnlineTrainer
{
    [TestClass]
    public class ModelValidatorTest
    {
        public TestContext TestContext { get; set; }
        private static string MLArgs1 = "--cb_explore_adf --epsilon 0.5";
        private static string MLArgs2 = "--multiworld_test abc --epsilon 0.5";

        private byte[] CreateModel(string args)
        {
            using var modelOutputTempFile = new TempFile();
            var exePath = TestConfiguration.TryGet(TestContext, "VwBinPath") ?? Resolver.ResolveVwBinary();
            IExeRunner exeRunner = new CommandLineExeRunner(exePath);
            exeRunner.Run($"--no_stdin --final_regressor={modelOutputTempFile.FilePath} {args}");
            return File.ReadAllBytes(modelOutputTempFile.FilePath);
        }
        private VwRunner GetVwRunner(string args = "")
        {
            using var modelOutputTempFile = new TempFile();
            var exePath = TestConfiguration.TryGet(TestContext, "VwBinPath") ?? Resolver.ResolveVwBinary();
            IExeRunner exeRunner = new CommandLineExeRunner(exePath);

            return new VwRunner(args, exeRunner);
        }

        [TestMethod]
        public async Task ModelValidator_MatchingModelAndMLArgsAsync()
        {
            var result = await GetVwRunner(MLArgs1).ValidateModelAsync(CreateModel(MLArgs1));
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public async Task ModelValidator_MisMatchingModelAndMLArgsAsync()
        {
            var result = await GetVwRunner(MLArgs2).ValidateModelAsync(CreateModel(MLArgs2));
            Assert.IsTrue(result.IsValid);
            var result2 = await GetVwRunner(MLArgs2).ValidateModelAsync(CreateModel(MLArgs1));
            Assert.IsFalse(result2.IsValid);
        }

        [TestMethod]
        public async Task ModelValidator_GarbageModelBytesAsync()
        {
            byte[] garbageModelBytes = { 0x1, 0x2, 0x3, 0x4 };
            var result = await GetVwRunner(MLArgs1).ValidateModelAsync(garbageModelBytes);
            Assert.IsFalse(result.IsValid);
        }

        [TestMethod]
        public async Task ModelValidator_GarbageMLArgsAsync()
        {
            var result = await GetVwRunner("asdfasdf").ValidateModelAsync(CreateModel(MLArgs1));
            Assert.IsFalse(result.IsValid);
        }

        [TestMethod]
        public async Task ModelValidator_NullAndEmptyMLArgsAsync()
        {
            // null ML args signals to VW to use the model embedded ML args
            var result = await GetVwRunner(null).ValidateModelAsync(CreateModel(MLArgs1));
            Assert.IsTrue(result.IsValid);

            // empty ML args signals to VW to use the model embedded ML args
            var result2 = await GetVwRunner("").ValidateModelAsync(CreateModel(MLArgs1));
            Assert.IsTrue(result2.IsValid);
        }

    }
}
