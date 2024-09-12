// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Utils.Exe;
using Microsoft.DecisionService.Instrumentation;
using Microsoft.DecisionService.VowpalWabbit;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VW.VWBinResolver;
using Moq;
using System;
using System.Text.RegularExpressions;

namespace DotNetCore.Tests.CommonVowpalWabbitTrainer
{
    [TestClass]
    public class VWExeOutputHandlerTests
    {
        [TestMethod]
        [DataRow("[info] Some message", "Some message", TracingLevel.Informational)]
        [DataRow("[warning] My [info] message", "My [info] message", TracingLevel.Warning)]
        [DataRow("[ErrOr] Some message", "Some message", TracingLevel.Error)]
        [DataRow("[CRITICAL]      ", "     ", TracingLevel.Critical)]
        [DataRow("[unknown] Some message", "Some message", TracingLevel.Verbose)]
        [DataRow("Some message", "Some message", TracingLevel.Verbose)]
        [DataRow("", "", TracingLevel.Verbose)]
        public void VWExeOutputHandler_HandleOutputLine(string line, string expectedMsg, TracingLevel expectedTracingLevel)
        {
            var moqLogger = new Mock<ILogger<VWExeOutputHandler>>();
            VWExeOutputHandler outputHandler = VWExeOutputHandler.Create(moqLogger.Object);

            outputHandler.HandleOutputLine(line);

            moqLogger.Verify(m => m.Log(
                It.Is<LogLevel>(level => level == expectedTracingLevel.ToLogLevel()),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == expectedMsg),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true))
            );
        }

        [TestMethod]
        public void VWExeOutputHandler_HandleOutputLine_Null()
        {
            var moqLogger = new Mock<ILogger<VWExeOutputHandler>>();
            VWExeOutputHandler outputHandler = VWExeOutputHandler.Create(moqLogger.Object);

            outputHandler.HandleOutputLine(null);

            moqLogger.Verify(m => m.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)
                ),
                Times.Never
            );
        }

        [TestMethod]
        [DataRow("[info] Some message", "Some message", TracingLevel.Informational)]
        [DataRow("[warning] My [info] message", "My [info] message", TracingLevel.Warning)]
        [DataRow("[ErrOr] Some message", "Some message", TracingLevel.Error)]
        [DataRow("[CRITICAL]      ", "     ", TracingLevel.Critical)]
        [DataRow("[unknown] Some message", "Some message", TracingLevel.Verbose)]
        [DataRow("Some message", "Some message", TracingLevel.Verbose)]
        [DataRow("", "", TracingLevel.Verbose)]
        public void VWExeOutputHandler_HandleErrorLine(string line, string expectedMsg, TracingLevel expectedTracingLevel)
        {
            var moqLogger = new Mock<ILogger<VWExeOutputHandler>>();
            VWExeOutputHandler outputHandler = VWExeOutputHandler.Create(moqLogger.Object);

            outputHandler.HandleErrorLine(line);

            moqLogger.Verify(m => m.Log(
                It.Is<LogLevel>(level => level == expectedTracingLevel.ToLogLevel()),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == expectedMsg),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true))
            );
        }

        [TestMethod]
        public void VWExeOutputHandler_HandleErrorLine_Null()
        {
            var moqLogger = new Mock<ILogger<VWExeOutputHandler>>();
            VWExeOutputHandler outputHandler = VWExeOutputHandler.Create(moqLogger.Object);

            outputHandler.HandleErrorLine(null);

            moqLogger.Verify(m => m.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)
                ),
                Times.Never
            );
        }

        [TestMethod]
        public void VWExeOutputHandler_Integration_NoStdIn()
        {
            var exeRunner = new CommandLineExeRunner(Resolver.ResolveVwBinary());
            var runResult = exeRunner.Run("--no_stdin");
            Assert.IsNotNull(runResult);
            Assert.AreEqual(0, runResult.ExitCode);
            Assert.IsTrue(string.IsNullOrEmpty(runResult.Output));
            Assert.IsFalse(string.IsNullOrEmpty(runResult.Error));
        }

        [TestMethod]
        public void VWExeOutputHandler_Integration_InvalidArgument()
        {
            var exeRunner = new CommandLineExeRunner(Resolver.ResolveVwBinary());
            var runResult = exeRunner.Run("--bad_argument");
            Assert.IsNotNull(runResult);
            Assert.AreEqual(1, runResult.ExitCode);
            Assert.IsTrue(string.IsNullOrEmpty(runResult.Error));
            Assert.IsFalse(string.IsNullOrEmpty(runResult.Output));
            Assert.IsTrue(Regex.IsMatch(runResult.Output, "\\[critical\\] vw [^)]*\\): unrecognised option '--bad_argument'"));
        }
    }
}
