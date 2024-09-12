// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommonTest;
using Microsoft.DecisionService.Instrumentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DecisionService.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCore.Tests.CommonMonitoring
{
    [TestClass]
    public class ExtensionMethodsTest
    {
        [TestMethod]
        public async Task ContinueWithPropagateCancellation_LogsCorrectlyWhenWhenAllCancelsAsync()
        { CancellationTokenSource cts = new CancellationTokenSource();
            Task task1 = Task.Run(() => { throw new Exception("task1 exception"); });
            Task task2 = Task.Run(() => { throw new Exception("task2 exception"); })
                .TraceAsync(NullLogger.Instance, "Exiting task 2");
            Task task3 = Task.Delay(-1, cts.Token).TraceAsync(NullLogger.Instance, "Exiting task3");

            Task comboTask = Task.WhenAny(task1, task2, task3)
                .ContinueWithPropagateCancellationAsync(cts, NullLogger.Instance);

            await comboTask;

            Assert.IsTrue(SpinWait.SpinUntil(
                () => task1.IsCompleted && task2.IsCompleted && task3.IsCompleted,
                3000));
            Assert.AreEqual(TaskStatus.Faulted, task1.Status);
            Assert.AreEqual(TaskStatus.Faulted, task2.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, task3.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, comboTask.Status);
        }
    }
}
