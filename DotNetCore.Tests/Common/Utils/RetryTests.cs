// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.DecisionService.Common.Error;
using Microsoft.DecisionService.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCore.Tests.Common.Utils
{
    [TestClass]
    // Do not parallelize these tests as they have a very short timeout value.
    // They are fragile when facing CPU thread scheduling.
    [DoNotParallelize]
    public class RetryTests
    {
        private const double SMALL_RETRY_DELAY_SEC = 0;
        private const double LARGE_RETRY_DELAY_SEC = 0.1;
        private const int SMALL_RUNTIME_CHECK_MS = 300;
        private const int LARGE_RUNTIME_CHECK_MS = 1000;

        #region SendRequestWithRetriesAsync tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task SendRequestWithRetriesAsync_NullFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.SendRequestWithRetriesAsync(null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task SendRequestWithRetriesAsync_NoRetries_Async(int retries)
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<AggregateException>(() =>
                Retry.SendRequestWithRetriesAsync(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new TaskCanceledException("message", new IOException());
                    }, retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task SendRequestWithRetriesAsync_ExceptionNotMatchDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                Retry.SendRequestWithRetriesAsync(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new InvalidOperationException();
                    })
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task SendRequestWithRetriesAsync_InnerExceptionNotMatchDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() =>
                Retry.SendRequestWithRetriesAsync(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new TaskCanceledException("foo", new ArgumentException());
                    })
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task SendRequestWithRetriesAsync_NoRetryDelay_Async(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int retries = 2;
            await Retry.SendRequestWithRetriesAsync(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return Task.FromResult(new HttpResponseMessage());
                    }

                    throw new TaskCanceledException("message", new IOException());
                },
                retries, retryDelayInSeconds);

            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task SendRequestWithRetriesAsync_SuccessDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            await Retry.SendRequestWithRetriesAsync(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(new HttpResponseMessage());
                });

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public async Task SendRequestWithRetriesAsync_MatchingExceptionRetriesWithDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            await Retry.SendRequestWithRetriesAsync(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new TaskCanceledException("message", new IOException());
                    }

                    return Task.FromResult(new HttpResponseMessage());
                },
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task SendRequestWithRetriesAsync_MatchingExceptionRetriesWithNoDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 3;
            await Retry.SendRequestWithRetriesAsync(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new TaskCanceledException("message", new IOException());
                    }

                    return Task.FromResult(new HttpResponseMessage());
                },
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        #endregion

        #region HandleInnerExceptionAsync tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleInnerExceptionAsync_NullFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleInnerExceptionAsync<int, Exception, IOException>(null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleInnerExceptionAsync_NoRetries_Async(int retries)
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<AggregateException>(() =>
                Retry.HandleInnerExceptionAsync<int, Exception, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new Exception("message", new InvalidOperationException());
                    }, retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleInnerExceptionAsync_ExceptionNotMatchDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                Retry.HandleInnerExceptionAsync<int, ArgumentException, IOException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new InvalidOperationException();
                    })
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleInnerExceptionAsync_InnerExceptionNotMatchDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<InvalidCastException>(() =>
                Retry.HandleInnerExceptionAsync<int, InvalidCastException, FormatException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new InvalidCastException("message", new PersonalizerException());
                    })
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleInnerExceptionAsync_NoRetryDelay_Async(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 100;
            const int retries = 2;
            int result = await Retry.HandleInnerExceptionAsync<int, AggregateException, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return Task.FromResult(expectedResult);
                    }

                    throw new AggregateException("message", new InvalidOperationException());
                },
                retries, retryDelayInSeconds);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleInnerExceptionAsync_SuccessDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const string expectedResult = "hello world";
            string result = await Retry.HandleInnerExceptionAsync<string, Exception, OutOfMemoryException>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                });

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public async Task HandleInnerExceptionAsync_MatchingExceptionRetriesWithDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimeFunctionsCalled = 3;
            const int expectedResult = 10;
            var sw = new Stopwatch();
            sw.Start();
            int result = await Retry.HandleInnerExceptionAsync<int, Exception, PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new Exception("message", new PersonalizerException());
                    }

                    return Task.FromResult(expectedResult);
                },
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleInnerExceptionAsync_MatchingExceptionRetriesWithNoDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 3;
            const float expectedResult = 100.1f;
            float result = await Retry.HandleInnerExceptionAsync<float, Exception, FormatException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new Exception("message", new FormatException());
                    }

                    return Task.FromResult(expectedResult);
                },
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        #endregion

        #region HandleExceptionAsync Overload 1 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload1_NullFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAsync<int>(null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload1_NoRetries_Async(int retries)
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<AggregateException>(() =>
                Retry.HandleExceptionAsync<int>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new Exception();
                    },
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload1_NoRetryDelay_Async(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 100;
            const int retries = 2;
            int result = await Retry.HandleExceptionAsync(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return Task.FromResult(expectedResult);
                    }

                    throw new IOException();
                },
                retries, retryDelayInSeconds);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload1_SuccessDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const float expectedResult = 1.0f;
            float result = await Retry.HandleExceptionAsync(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                });

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload1_MatchingExceptionRetriesWithDelay_Async()
        {
            int timesFunctionCalled = 0;
            const string expectedResult = "hello world";
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            string result = await Retry.HandleExceptionAsync(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return Task.FromResult(expectedResult);
                },
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload1_MatchingExceptionRetriesWithNoDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAsync(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.FromResult(expectedResult);
                },
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        #endregion

        #region HandleExceptionAsync Overload 2 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload2_NullFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAsync<int, TaskCanceledException>(null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload2_NoRetries_Async(int retries)
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<AggregateException>(() =>
                Retry.HandleExceptionAsync<int, TaskCanceledException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new TaskCanceledException();
                    }, retries, 0)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload2_NoRetryDelay_Async(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 100;
            const int retries = 2;
            int result = await Retry.HandleExceptionAsync<int, IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return Task.FromResult(expectedResult);
                    }

                    throw new IOException();
                },
                retries, retryDelayInSeconds);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload2_SuccessDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const float expectedResult = 1.0f;
            float result = await Retry.HandleExceptionAsync<float, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                });

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload2_MatchingExceptionRetriesWithDelay_Async()
        {
            int timesFunctionCalled = 0;
            const string expectedResult = "hello world";
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            string result = await Retry.HandleExceptionAsync<string, PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return Task.FromResult(expectedResult);
                },
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload2_MatchingExceptionRetriesWithNoDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAsync<int, NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.FromResult(expectedResult);
                },
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload2_RetrySubclassException_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAsync<int, Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.FromResult(expectedResult);
                },
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload2_NotMatchingExceptionDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 1;
            await Assert.ThrowsExceptionAsync<NullReferenceException>(() =>
                Retry.HandleExceptionAsync<int, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new NullReferenceException();
                    })
            );

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        #endregion

        #region HandleExceptionAsync Overload 3 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload3_NullFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAsync<int, TaskCanceledException>(
                    null, tce => true));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload3_NullShouldRetryFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAsync<int, TaskCanceledException>(
                    () => Task.FromResult(10), null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload3_NoRetries_Async(int retries)
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<AggregateException>(() =>
                Retry.HandleExceptionAsync<int, TaskCanceledException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new TaskCanceledException();
                    },
                    e => true,
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload3_NoRetryDelay_Async(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 100;
            const int retries = 2;
            int result = await Retry.HandleExceptionAsync<int, IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return Task.FromResult(expectedResult);
                    }

                    throw new IOException();
                },
                e => true,
                retries, retryDelayInSeconds);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload3_SuccessDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const float expectedResult = 1.0f;
            float result = await Retry.HandleExceptionAsync<float, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                },
                e => true);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload3_MatchingExceptionRetriesWithDelay_Async()
        {
            int timesFunctionCalled = 0;
            const string expectedResult = "hello world";
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            string result = await Retry.HandleExceptionAsync<string, PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return Task.FromResult(expectedResult);
                },
                e => true,
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload3_MatchingExceptionRetriesWithNoDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAsync<int, NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.FromResult(expectedResult);
                },
                e => true,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload3_RetrySubclassException_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAsync<int, Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.FromResult(expectedResult);
                },
                e => true,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload3_NotMatchingExceptionDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 1;
            await Assert.ThrowsExceptionAsync<NullReferenceException>(() =>
                Retry.HandleExceptionAsync<int, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new NullReferenceException();
                    },
                    e => true)
            );

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload3_DoNotRetryWhenConditionFalse_Async()
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                Retry.HandleExceptionAsync<int, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new InvalidOperationException();
                    },
                    e => false)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        #endregion

        #region HandleExceptionAsync Overload 4 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload4_NullFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAsync<Exception>(null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload4_NoRetries_Async(int retries)
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<AggregateException>(() =>
                Retry.HandleExceptionAsync<Exception>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new Exception();
                    },
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload4_NoRetryDelay_Async(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int retries = 2;
            await Retry.HandleExceptionAsync<Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return Task.CompletedTask;
                    }

                    throw new IOException();
                },
                retries, retryDelayInSeconds);

            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload4_SuccessDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            await Retry.HandleExceptionAsync<Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.CompletedTask;
                });

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload4_MatchingExceptionRetriesWithDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            await Retry.HandleExceptionAsync<PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return Task.CompletedTask;
                },
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload4_MatchingExceptionRetriesWithNoDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 3;
            await Retry.HandleExceptionAsync<NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.CompletedTask;
                },
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        #endregion

        #region HandleExceptionAsync Overload 5 tests

        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload5_NullFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAsync<TaskCanceledException>(
                    null, tce => true));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload5_NullShouldRetryFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAsync<TaskCanceledException>(
                    () => Task.CompletedTask, null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload5_NoRetries_Async(int retries)
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<AggregateException>(() =>
                Retry.HandleExceptionAsync<TaskCanceledException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new TaskCanceledException();
                    },
                    e => true,
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload5_NoRetryDelay_Async(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int retries = 2;
            await Retry.HandleExceptionAsync<IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return Task.CompletedTask;
                    }

                    throw new IOException();
                },
                e => true,
                retries, retryDelayInSeconds);

            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload5_SuccessDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const float expectedResult = 1.0f;
            await Retry.HandleExceptionAsync<InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                },
                e => true);

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload5_MatchingExceptionRetriesWithDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            await Retry.HandleExceptionAsync<PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return Task.CompletedTask;
                },
                e => true,
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload5_MatchingExceptionRetriesWithNoDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 3;
            await Retry.HandleExceptionAsync<NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.CompletedTask;
                },
                e => true,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload5_RetrySubclassException_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 3;
            await Retry.HandleExceptionAsync<Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.CompletedTask;
                },
                e => true,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload5_NotMatchingExceptionDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 1;
            await Assert.ThrowsExceptionAsync<NullReferenceException>(() =>
                Retry.HandleExceptionAsync<InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new NullReferenceException();
                    },
                    e => true)
            );

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAsync_Overload5_DoNotRetryWhenConditionFalse_Async()
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                Retry.HandleExceptionAsync<InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new InvalidOperationException();
                    },
                    e => false)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        #endregion

        #region HandleException Overload 1 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload1_NullFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleException<int, Exception>(null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload1_NoRetries(int retries)
        {
            int timesFunctionCalled = 0;
            Assert.ThrowsException<AggregateException>(() =>
                Retry.HandleException<int, Exception>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new Exception();
                    },
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload1_NoRetryDelay(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 100;
            const int retries = 2;
            int result = Retry.HandleException<int, IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return expectedResult;
                    }

                    throw new IOException();
                },
                retries, retryDelayInSeconds);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload1_SuccessDoesNotRetry()
        {
            int timesFunctionCalled = 0;
            const float expectedResult = 1.0f;
            float result = Retry.HandleException<float, Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    return expectedResult;
                });

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public void HandleException_Overload1_MatchingExceptionRetriesWithDelay()
        {
            int timesFunctionCalled = 0;
            const string expectedResult = "hello world";
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            string result = Retry.HandleException<string, PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return expectedResult;
                },
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload1_MatchingExceptionRetriesWithNoDelay()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = Retry.HandleException<int, NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return expectedResult;
                },
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        #endregion

        #region HandleException Overload 2 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload2_NullFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleException<int, Exception>(
                    null, e => true));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload2_NullShouldRetryFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleException<int, TaskCanceledException>(
                    () => 10, null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload2_NoRetries(int retries)
        {
            int timesFunctionCalled = 0;
            Assert.ThrowsException<AggregateException>(() =>
                Retry.HandleException<int, Exception>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new Exception();
                    },
                    e => true,
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload2_NoRetryDelay(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 100;
            const int retries = 2;
            int result = Retry.HandleException<int, IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return expectedResult;
                    }

                    throw new IOException();
                },
                e => true,
                retries, retryDelayInSeconds);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload2_SuccessDoesNotRetry()
        {
            int timesFunctionCalled = 0;
            const float expectedResult = 1.0f;
            float result = Retry.HandleException<float, Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    return expectedResult;
                },
                e => true);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public void HandleException_Overload2_MatchingExceptionRetriesWithDelay()
        {
            int timesFunctionCalled = 0;
            const string expectedResult = "hello world";
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            string result = Retry.HandleException<string, PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return expectedResult;
                },
                e => true,
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload2_MatchingExceptionRetriesWithNoDelay()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = Retry.HandleException<int, NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return expectedResult;
                },
                e => true,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload2_DoNotRetryWhenConditionFalse()
        {
            int timesFunctionCalled = 0;
            Assert.ThrowsException<InvalidOperationException>(() =>
                Retry.HandleException<int, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new InvalidOperationException();
                    },
                    e => false)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        #endregion

        #region HandleException Overload 3 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload3_NullFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleException<Exception>(null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload3_NoRetries(int retries)
        {
            int timesFunctionCalled = 0;
            Assert.ThrowsException<AggregateException>(() =>
                Retry.HandleException<Exception>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new Exception();
                    },
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload3_NoRetryDelay(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int retries = 2;
            Retry.HandleException<IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return;
                    }

                    throw new IOException();
                },
                retries, retryDelayInSeconds);

            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload3_SuccessDoesNotRetry()
        {
            int timesFunctionCalled = 0;
            Retry.HandleException<Exception>(
                () => { timesFunctionCalled++; });

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public void HandleException_Overload3_MatchingExceptionRetriesWithDelay()
        {
            int timesFunctionCalled = 0;
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            Retry.HandleException<PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }
                },
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload3_MatchingExceptionRetriesWithNoDelay()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 3;
            Retry.HandleException<NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }
                },
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        #endregion

        #region HandleException Overload 4 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload4_NullFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleException<Exception>(null));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload4_NullShouldRetryFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleException<TaskCanceledException>(
                    () => { }, null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload4_NoRetries(int retries)
        {
            int timesFunctionCalled = 0;
            Assert.ThrowsException<AggregateException>(() =>
                Retry.HandleException<Exception>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new Exception();
                    },
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload4_NoRetryDelay(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int retries = 2;
            Retry.HandleException<IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return;
                    }

                    throw new IOException();
                },
                retries, retryDelayInSeconds);

            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload4_SuccessDoesNotRetry()
        {
            int timesFunctionCalled = 0;
            Retry.HandleException<Exception>(
                () => { timesFunctionCalled++; });

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public void HandleException_Overload4_MatchingExceptionRetriesWithDelay()
        {
            int timesFunctionCalled = 0;
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            Retry.HandleException<PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }
                },
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload4_MatchingExceptionRetriesWithNoDelay()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 3;
            Retry.HandleException<NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }
                },
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleException_Overload4_DoNotRetryWhenConditionFalse()
        {
            int timesFunctionCalled = 0;
            Assert.ThrowsException<InvalidOperationException>(() =>
                Retry.HandleException<InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new InvalidOperationException();
                    },
                    e => false)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        #endregion

        #region HandleExceptionAndConditionAsync Overload 1 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_NullFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAndConditionAsync<int, TaskCanceledException>(
                    null, r => true));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_NullShouldRetryFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAndConditionAsync<int, TaskCanceledException>(
                    () => Task.FromResult(10), null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_NoRetries_Async(int retries)
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<AggregateException>(() =>
                Retry.HandleExceptionAndConditionAsync<int, TaskCanceledException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new TaskCanceledException();
                    },
                    r => true,
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_NoRetryDelay_Async(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 100;
            const int retries = 2;
            int result = await Retry.HandleExceptionAndConditionAsync<int, IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return Task.FromResult(expectedResult);
                    }

                    throw new IOException();
                },
                r => true,
                retries, retryDelayInSeconds);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_SuccessDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const float expectedResult = 1.0f;
            float result = await Retry.HandleExceptionAndConditionAsync<float, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                },
                r => false);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_MatchingExceptionRetriesWithDelay_Async()
        {
            int timesFunctionCalled = 0;
            const string expectedResult = "hello world";
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            string result = await Retry.HandleExceptionAndConditionAsync<string, PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return Task.FromResult(expectedResult);
                },
                r => false,
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_MatchingExceptionRetriesWithNoDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAndConditionAsync<int, NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.FromResult(expectedResult);
                },
                r => false,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_RetrySubclassException_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAndConditionAsync<int, Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.FromResult(expectedResult);
                },
                r => false,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_NotMatchingExceptionDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 1;
            await Assert.ThrowsExceptionAsync<NullReferenceException>(() =>
                Retry.HandleExceptionAndConditionAsync<int, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new NullReferenceException();
                    },
                    r => true)
            );

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_DoNotRetryWhenConditionFalse_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 1;
            int result = await Retry.HandleExceptionAndConditionAsync<int, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                },
                r => false);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload1_RetryWhenConditionTrue_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAndConditionAsync<int, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                },
                r => true,
                expectedTimesFunctionCalled,
                SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        #endregion

        #region HandleExceptionAndConditionAsync Overload 2 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_NullFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAndConditionAsync<int, TaskCanceledException>(
                    null, r => true, e => true));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_NullShouldRetryFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAndConditionAsync<int, TaskCanceledException>(
                    () => Task.FromResult(10), null, e => true));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_NullShouldRetryExceptionFunctionThrows_Async()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => Retry.HandleExceptionAndConditionAsync<int, TaskCanceledException>(
                    () => Task.FromResult(10), r => true, null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_NoRetries_Async(int retries)
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<AggregateException>(() =>
                Retry.HandleExceptionAndConditionAsync<int, TaskCanceledException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new TaskCanceledException();
                    },
                    r => true,
                    e => true,
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_NoRetryDelay_Async(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 100;
            const int retries = 2;
            int result = await Retry.HandleExceptionAndConditionAsync<int, IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return Task.FromResult(expectedResult);
                    }

                    throw new IOException();
                },
                r => true,
                e => true,
                retries, retryDelayInSeconds);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_SuccessDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const float expectedResult = 1.0f;
            float result = await Retry.HandleExceptionAndConditionAsync<float, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                },
                r => false,
                e => true);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_MatchingExceptionRetriesWithDelay_Async()
        {
            int timesFunctionCalled = 0;
            const string expectedResult = "hello world";
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            string result = await Retry.HandleExceptionAndConditionAsync<string, PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return Task.FromResult(expectedResult);
                },
                r => false,
                e => true,
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_MatchingExceptionRetriesWithNoDelay_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAndConditionAsync<int, NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.FromResult(expectedResult);
                },
                r => false,
                e => true,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_RetrySubclassException_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAndConditionAsync<int, Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return Task.FromResult(expectedResult);
                },
                r => false,
                e => true,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_NotMatchingExceptionDoesNotRetry_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 1;
            await Assert.ThrowsExceptionAsync<NullReferenceException>(() =>
                Retry.HandleExceptionAndConditionAsync<int, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new NullReferenceException();
                    },
                    e => true,
                    r => true)
            );

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_DoNotRetryWhenConditionFalse_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 1;
            int result = await Retry.HandleExceptionAndConditionAsync<int, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                },
                r => false,
                e => true);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_RetryWhenConditionTrue_Async()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 1;
            const int expectedTimesFunctionCalled = 3;
            int result = await Retry.HandleExceptionAndConditionAsync<int, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return Task.FromResult(expectedResult);
                },
                r => true,
                expectedTimesFunctionCalled,
                SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public async Task HandleExceptionAndConditionAsync_Overload2_DoNotRetryExceptionWhenConditionFalse_Async()
        {
            int timesFunctionCalled = 0;
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                Retry.HandleExceptionAndConditionAsync<int, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new InvalidOperationException();
                    },
                    r => true,
                    e => false)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        #endregion

        #region HandleExceptionAndCondition Overload 1 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_NullFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleExceptionAndCondition<int, TaskCanceledException>(
                    null, r => true));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_NullShouldRetryFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleExceptionAndCondition<int, TaskCanceledException>(
                    () => 10, null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_NoRetries(int retries)
        {
            int timesFunctionCalled = 0;
            Assert.ThrowsException<AggregateException>(() =>
                Retry.HandleExceptionAndCondition<int, TaskCanceledException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new TaskCanceledException();
                    },
                    r => true,
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_NoRetryDelay(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 100;
            const int retries = 2;
            int result = Retry.HandleExceptionAndCondition<int, IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return expectedResult;
                    }

                    throw new IOException();
                },
                r => true,
                retries, retryDelayInSeconds);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_SuccessDoesNotRetry()
        {
            int timesFunctionCalled = 0;
            const float expectedResult = 1.0f;
            float result = Retry.HandleExceptionAndCondition<float, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return expectedResult;
                },
                r => false);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_MatchingExceptionRetriesWithDelay()
        {
            int timesFunctionCalled = 0;
            const string expectedResult = "hello world";
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            string result = Retry.HandleExceptionAndCondition<string, PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return expectedResult;
                },
                r => false,
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_MatchingExceptionRetriesWithNoDelay()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = Retry.HandleExceptionAndCondition<int, NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return expectedResult;
                },
                r => false,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_RetrySubclassException()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = Retry.HandleExceptionAndCondition<int, Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return expectedResult;
                },
                r => false,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_NotMatchingExceptionDoesNotRetry()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 1;
            Assert.ThrowsException<NullReferenceException>(() =>
                Retry.HandleExceptionAndCondition<int, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new NullReferenceException();
                    },
                    r => true)
            );

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_DoNotRetryWhenConditionFalse()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 1;
            int result = Retry.HandleExceptionAndCondition<int, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return expectedResult;
                },
                r => false);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload1_RetryWhenConditionTrue()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 1;
            const int expectedTimesFunctionCalled = 3;
            int result = Retry.HandleExceptionAndCondition<int, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return expectedResult;
                },
                r => true,
                expectedTimesFunctionCalled,
                SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        #endregion

        #region HandleExceptionAndCondition Overload 2 tests

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_NullFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleExceptionAndCondition<int, TaskCanceledException>(
                    null, r => true, e => true));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_NullShouldRetryFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleExceptionAndCondition<int, TaskCanceledException>(
                    () => 10, null, e => true));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_NullShouldRetryExceptionFunctionThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => Retry.HandleExceptionAndCondition<int, TaskCanceledException>(
                    () => 10, r => true, null));
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_NoRetries(int retries)
        {
            int timesFunctionCalled = 0;
            Assert.ThrowsException<AggregateException>(() =>
                Retry.HandleExceptionAndCondition<int, TaskCanceledException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new TaskCanceledException();
                    },
                    r => true,
                    e => true,
                    retries, SMALL_RETRY_DELAY_SEC)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(0)]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_NoRetryDelay(double retryDelayInSeconds)
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 100;
            const int retries = 2;
            int result = Retry.HandleExceptionAndCondition<int, IOException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled == retries)
                    {
                        return expectedResult;
                    }

                    throw new IOException();
                },
                r => true,
                e => true,
                retries, retryDelayInSeconds);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(retries, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_SuccessDoesNotRetry()
        {
            int timesFunctionCalled = 0;
            const float expectedResult = 1.0f;
            float result = Retry.HandleExceptionAndCondition<float, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return expectedResult;
                },
                r => false,
                e => true);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(LARGE_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_MatchingExceptionRetriesWithDelay()
        {
            int timesFunctionCalled = 0;
            const string expectedResult = "hello world";
            const int expectedTimeFunctionsCalled = 3;
            var sw = new Stopwatch();
            sw.Start();
            string result = Retry.HandleExceptionAndCondition<string, PersonalizerException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimeFunctionsCalled)
                    {
                        throw new PersonalizerException();
                    }

                    return expectedResult;
                },
                r => false,
                e => true,
                retryDelayInSeconds: LARGE_RETRY_DELAY_SEC);
            sw.Stop();

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimeFunctionsCalled, timesFunctionCalled);
            Assert.IsTrue(sw.ElapsedMilliseconds > LARGE_RETRY_DELAY_SEC * (timesFunctionCalled - 1));
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_MatchingExceptionRetriesWithNoDelay()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = Retry.HandleExceptionAndCondition<int, NullReferenceException>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return expectedResult;
                },
                r => false,
                e => true,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_RetrySubclassException()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = -1;
            const int expectedTimesFunctionCalled = 3;
            int result = Retry.HandleExceptionAndCondition<int, Exception>(
                () =>
                {
                    timesFunctionCalled++;
                    if (timesFunctionCalled < expectedTimesFunctionCalled)
                    {
                        throw new NullReferenceException();
                    }

                    return expectedResult;
                },
                r => false,
                e => true,
                retryDelayInSeconds: SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_NotMatchingExceptionDoesNotRetry()
        {
            int timesFunctionCalled = 0;
            const int expectedTimesFunctionCalled = 1;
            Assert.ThrowsException<NullReferenceException>(() =>
                Retry.HandleExceptionAndCondition<int, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new NullReferenceException();
                    },
                    e => true,
                    r => true)
            );

            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_DoNotRetryWhenConditionFalse()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 1;
            int result = Retry.HandleExceptionAndCondition<int, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return expectedResult;
                },
                r => false,
                e => true);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(1, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_RetryWhenConditionTrue()
        {
            int timesFunctionCalled = 0;
            const int expectedResult = 1;
            const int expectedTimesFunctionCalled = 3;
            int result = Retry.HandleExceptionAndCondition<int, InvalidOperationException>(
                () =>
                {
                    timesFunctionCalled++;
                    return expectedResult;
                },
                r => true,
                expectedTimesFunctionCalled,
                SMALL_RETRY_DELAY_SEC);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedTimesFunctionCalled, timesFunctionCalled);
        }

        [TestMethod]
        [Timeout(SMALL_RUNTIME_CHECK_MS)]
        public void HandleExceptionAndCondition_Overload2_DoNotRetryExceptionWhenConditionFalse()
        {
            int timesFunctionCalled = 0;
            Assert.ThrowsException<InvalidOperationException>(() =>
                Retry.HandleExceptionAndCondition<int, InvalidOperationException>(
                    () =>
                    {
                        timesFunctionCalled++;
                        throw new InvalidOperationException();
                    },
                    r => true,
                    e => false)
            );

            Assert.AreEqual(1, timesFunctionCalled);
        }

        #endregion
    }
}
