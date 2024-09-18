// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DecisionService.Common.Utils.Exe;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Common.Utils.Exe
{
    [TestClass]
    public class CommandLineExeRunnerTests
    {
        private const string WIN_BINARY = "TestExeApp.exe";
        private const string NIX_BINARY = "TestExeApp";

        private static string GetBinaryFullPath(string exeName)
        {
            return Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), exeName);
        }

        private static string GetTestAppBinary()
        {
            bool isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string platformSpecificExe = isWindowsOS ? WIN_BINARY : NIX_BINARY;
            string binFile = GetBinaryFullPath(platformSpecificExe);
            if (File.Exists(binFile)) {
                return binFile;
            }
            string fallbackExe = isWindowsOS ? NIX_BINARY : WIN_BINARY;
            binFile = GetBinaryFullPath(fallbackExe);
            if (File.Exists(binFile)) {
                return binFile;
            }
            throw new FileNotFoundException("TestExeApp or TestExeApp.exe was not found", binFile);
        }

        #region RunAsync tests

        [TestMethod]
        public async Task RunAsync_NoParams_Async()
        {
            IExeRunner exeRunner = new CommandLineExeRunner(GetTestAppBinary());
            var result = await exeRunner.RunAsync();
            Assert.AreEqual(0, result.ExitCode);
        }

        [DataTestMethod]
        [DataRow(255)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task RunAsync_ReturnExitCode_Async(int expectedExitCode)
        {
            IExeRunner exeRunner = new CommandLineExeRunner(GetTestAppBinary());
            var result = await exeRunner.RunAsync(
                $"--exit-code {expectedExitCode}");
            Assert.AreEqual(expectedExitCode, result.ExitCode);
        }

        [TestMethod]
        public async Task RunAsync_WithInput_Async()
        {
            const string expectedOutput = "output message";
            IExeRunner exeRunner = new CommandLineExeRunner(GetTestAppBinary());

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
            IExeRunner exeRunner = new CommandLineExeRunner(GetTestAppBinary());
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
            IExeRunner exeRunner = new CommandLineExeRunner(GetTestAppBinary());
            var result = exeRunner.Run();
            Assert.AreEqual(0, result.ExitCode);
        }

        [DataTestMethod]
        [DataRow(255)]
        [DataRow(1)]
        [DataRow(2)]
        public void Run_ReturnExitCode(int expectedExitCode)
        {
            IExeRunner exeRunner = new CommandLineExeRunner(GetTestAppBinary());
            var result = exeRunner.Run(
                $"--exit-code {expectedExitCode}");
            Assert.AreEqual(expectedExitCode, result.ExitCode);
        }

        [TestMethod]
        public void Run_WithInput()
        {
            const string expectedOutput = "output message";
            IExeRunner exeRunner = new CommandLineExeRunner(GetTestAppBinary());

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
            IExeRunner exeRunner = new CommandLineExeRunner(GetTestAppBinary());

            var result = exeRunner.Run(
                $"--output \"{expectedOutput}\" --error \"{expectedError}\"");

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual(expectedOutput, result.Output.Trim());
            Assert.AreEqual(expectedError, result.Error.Trim());
        }

        #endregion
    }
}