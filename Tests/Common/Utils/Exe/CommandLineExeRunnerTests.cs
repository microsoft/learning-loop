// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DecisionService.Common.Utils.Exe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Common.Utils.Exe
{
    [TestClass]
    public class CommandLineExeRunnerTests
    {
        private const string VW_MOCK_BINARY = "TestExeApp.dll";

        private static string GetBinaryFullPath(string exeName)
        {
            return Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), exeName);
        }

        private static IExeRunner GetCommandLineRunner()
        {
            return new CommandLineExeRunner(GetBinaryFullPath(VW_MOCK_BINARY), dotnetRunner: true);
        }

        #region RunAsync tests

        [TestMethod]
        public async Task RunAsync_NoParams_Async()
        {
            IExeRunner exeRunner = GetCommandLineRunner();
            var result = await exeRunner.RunAsync();
            Assert.AreEqual(0, result.ExitCode);
        }

        [DataTestMethod]
        [DataRow(255)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task RunAsync_ReturnExitCode_Async(int expectedExitCode)
        {
            IExeRunner exeRunner = GetCommandLineRunner();
            var result = await exeRunner.RunAsync(
                $"--exit-code {expectedExitCode}");
            Assert.AreEqual(expectedExitCode, result.ExitCode);
        }

        [TestMethod]
        public async Task RunAsync_WithInput_Async()
        {
            const string expectedOutput = "output message";
            IExeRunner exeRunner = GetCommandLineRunner();

            var result = await exeRunner.RunAsync(
                "--use-stdin",
                Encoding.UTF8.GetBytes(expectedOutput));

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual(expectedOutput, result.Output.Trim());
        }

        [TestMethod]
        [Timeout(6000)]
        public async Task RunAsync_CancelLongRunningProcess_Async()
        {
            IExeRunner exeRunner = GetCommandLineRunner();
            using var cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            cancellationTokenSource.Cancel();
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() =>
                exeRunner.RunAsync(
                    $"--sleep {5000}",
                    cancellationToken: cancellationToken));
        }

        #endregion

        #region Run tests

        [TestMethod]
        public void Run_NoParams()
        {
            IExeRunner exeRunner = GetCommandLineRunner();
            var result = exeRunner.Run();
            Assert.AreEqual(0, result.ExitCode);
        }

        [DataTestMethod]
        [DataRow(255)]
        [DataRow(1)]
        [DataRow(2)]
        public void Run_ReturnExitCode(int expectedExitCode)
        {
            IExeRunner exeRunner = GetCommandLineRunner();
            var result = exeRunner.Run(
                $"--exit-code {expectedExitCode}");
            Assert.AreEqual(expectedExitCode, result.ExitCode);
        }

        [TestMethod]
        public void Run_WithInput()
        {
            const string expectedOutput = "output message";
            IExeRunner exeRunner = GetCommandLineRunner();

            var result = exeRunner.Run(
                "--use-stdin",
                Encoding.UTF8.GetBytes(expectedOutput));

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual(expectedOutput, result.Output.Trim());
        }

        [TestMethod]
        public void Run_OutputHandlerCalled()
        {
            const string expectedOutput = "output message";
            const string expectedError = "error message";
            IExeRunner exeRunner = GetCommandLineRunner();

            var result = exeRunner.Run(
                $"--output \"{expectedOutput}\" --error \"{expectedError}\"");

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual(expectedOutput, result.Output.Trim());
            Assert.AreEqual(expectedError, result.Error.Trim());
        }

        #endregion
    }
}