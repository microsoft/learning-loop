// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Utils
{
    public static class Retry
    {
        /// <summary>
        /// Retry the function even if an exception is thrown.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static Task<HttpResponseMessage> SendRequestWithRetriesAsync(
            Func<Task<HttpResponseMessage>> f,
            int retries = 5,
            double retryDelayInSeconds = 5)
        {
            return HandleInnerExceptionAsync<HttpResponseMessage, TaskCanceledException, IOException>(f, retries,
                retryDelayInSeconds);
        }

        /// <summary>
        /// Retry the function even if an exception is thrown.
        /// E is defined as a the exception to handle, with an additional check for InnerException I.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static async Task<T> HandleInnerExceptionAsync<T, E, I>(
            Func<Task<T>> f,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception where I : Exception
        {
            CheckParams(f);

            int retriesRemaining = retries;
            List<Exception> exceptions = new List<Exception>();
            do
            {
                try
                {
                    return await f.Invoke().ConfigureAwait(false);
                }
                catch (E e) when (e.InnerException is I)
                {
                    exceptions.Insert(0, e);
                    if (retryDelayInSeconds > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(retryDelayInSeconds)).ConfigureAwait(false);
                    }
                }
            } while (--retriesRemaining > 0);

            throw new AggregateException(exceptions);
        }

        /// <summary>
        /// Retry the function even if an exception is thrown.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static Task HandleExceptionAsync<T>(Action<Task> f, int retries = 5, double retryDelayInSeconds = 5)
        {
            return HandleExceptionAsync<Exception>(f, retries, retryDelayInSeconds);
        }

        /// <summary>
        /// Retry the function even if an exception is thrown.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static Task<T> HandleExceptionAsync<T>(Func<Task<T>> f, int retries = 5, double retryDelayInSeconds = 5)
        {
            return HandleExceptionAsync<T, Exception>(f, retries, retryDelayInSeconds);
        }

        /// <summary>
        /// Retry the function even if an exception is thrown. E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        public static Task<T> HandleExceptionAsync<T, E>(
            Func<Task<T>> f,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            return HandleExceptionAsync<T, E>(f, e => true, retries, retryDelayInSeconds);
        }

        /// <summary>
        /// Retry the function if an exception is thrown, and shouldRetryException evaluates to true.
        /// E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="shouldRetryException">Function that determines if function f should be retried.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static async Task<T> HandleExceptionAsync<T, E>(
            Func<Task<T>> f,
            Func<E, bool> shouldRetryException,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            CheckParams(f, shouldRetryException);

            int retriesRemaining = retries;
            List<Exception> exceptions = new List<Exception>();
            do
            {
                try
                {
                    return await f.Invoke().ConfigureAwait(false);
                }
                catch (E e)
                {
                    if (!shouldRetryException(e))
                    {
                        throw;
                    }

                    exceptions.Insert(0, e);
                    if (retryDelayInSeconds > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(retryDelayInSeconds)).ConfigureAwait(false);
                    }
                }
            } while (--retriesRemaining > 0);

            throw new AggregateException(exceptions);
        }

        /// <summary>
        /// Retry the function if an exception is thrown, and shouldRetryException evaluates to true.
        /// E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static Task HandleExceptionAsync<E>(
            Func<Task> f,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            return HandleExceptionAsync<E>(f, e => true, retries, retryDelayInSeconds);
        }

        /// <summary>
        /// Retry the function if an exception is thrown, and shouldRetryException evaluates to true.
        /// E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="shouldRetryException">Function that determines if function f should be retried.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static async Task HandleExceptionAsync<E>(
            Func<Task> f,
            Func<E, bool> shouldRetryException,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            CheckParams(f, shouldRetryException);

            int retriesRemaining = retries;
            var exceptions = new List<Exception>();
            do
            {
                try
                {
                    await f.Invoke().ConfigureAwait(false);
                    return;
                }
                catch (E e)
                {
                    if (!shouldRetryException(e))
                    {
                        throw;
                    }

                    exceptions.Insert(0, e);
                    if (retryDelayInSeconds > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(retryDelayInSeconds)).ConfigureAwait(false);
                    }
                }
            } while (--retriesRemaining > 0);

            throw new AggregateException(exceptions);
        }

        /// <summary>
        /// Retry the function if an exception is thrown.
        /// E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static T HandleException<T, E>(
            Func<T> f,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            return HandleException<T, E>(f, e => true, retries, retryDelayInSeconds);
        }

        /// <summary>
        /// Retry the function if an exception is thrown, and shouldRetryException evaluates to true. E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="shouldRetryException">Function that determines if function f should be retried.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static T HandleException<T, E>(
            Func<T> f,
            Func<E, bool> shouldRetryException,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            CheckParams(f, shouldRetryException);

            int retriesRemaining = retries;
            var exceptions = new List<Exception>();
            do
            {
                try
                {
                    return f.Invoke();
                }
                catch (E e)
                {
                    if (!shouldRetryException(e))
                    {
                        throw;
                    }

                    exceptions.Insert(0, e);
                    if (retryDelayInSeconds > 0)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(retryDelayInSeconds));
                    }
                }
            } while (--retriesRemaining > 0);

            throw new AggregateException(exceptions);
        }

        /// <summary>
        /// Retry the function if an exception is thrown. E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static void HandleException<E>(
            Action f,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            HandleException<E>(f, e => true, retries, retryDelayInSeconds);
        }

        /// <summary>
        /// Retry the function if an exception is thrown.
        /// E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="shouldRetry">Function that determines if function f should be retried.</param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static void HandleException<E>(
            Action f,
            Func<E, bool> shouldRetry,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            CheckParams(f, shouldRetry);

            int retriesRemaining = retries;
            var exceptions = new List<Exception>();
            do
            {
                try
                {
                    f.Invoke();
                    return;
                }
                catch (E e)
                {
                    if (!shouldRetry(e))
                    {
                        throw;
                    }

                    exceptions.Insert(0, e);
                    if (retryDelayInSeconds > 0)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(retryDelayInSeconds));
                    }
                }
            } while (--retriesRemaining > 0);

            throw new AggregateException(exceptions);
        }

        /// <summary>
        /// Retry the function if an exception is thrown or the desired result in not reached.
        /// shouldRetryException indicates if the result should be retried.
        /// E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="shouldRetry">
        /// Function that determines if function f should be retried.
        /// If false the result is returned.
        /// </param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static Task<T> HandleExceptionAndConditionAsync<T, E>(
            Func<Task<T>> f,
            Func<T, bool> shouldRetry,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            return HandleExceptionAndConditionAsync<T, E>(f, shouldRetry, e => true, retries,
                retryDelayInSeconds);
        }

        /// <summary>
        /// Retry the function if an exception is thrown or the desired result in not reached.
        /// shouldRetryException indicates if the result should be retried.
        /// shouldRetryException indicates if the exception is able to be retried.
        /// E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="shouldRetry">
        /// Function that determines if function f should be retried.
        /// If false the result is returned.
        /// </param>
        /// <param name="shouldRetryException">
        /// Function that determines if function f should be retried when an exception is
        /// thrown. If false the exception is thrown.
        /// </param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static async Task<T> HandleExceptionAndConditionAsync<T, E>(
            Func<Task<T>> f,
            Func<T, bool> shouldRetry,
            Func<E, bool> shouldRetryException,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            CheckParams(f, shouldRetry, shouldRetryException);

            int retriesRemaining = retries;
            var exceptions = new List<Exception>();
            var result = default(T);
            bool hasResult = false;
            do
            {
                try
                {
                    result = await f.Invoke().ConfigureAwait(false);
                    hasResult = true;
                    if (!shouldRetry(result)) return result;
                }
                catch (E e)
                {
                    if (!shouldRetryException(e)) throw;
                    exceptions.Insert(0, e);
                }

                if (retryDelayInSeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(retryDelayInSeconds)).ConfigureAwait(false);
                }
            } while (--retriesRemaining > 0);

            if (hasResult) return result;
            throw new AggregateException(exceptions);
        }

        /// <summary>
        /// Retry the function if an exception is thrown or the desired result in not reached.
        /// shouldRetryException indicates if the result should be retried.
        /// E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="shouldRetry">
        /// Function that determines if function f should be retried.
        /// If false the result is returned.
        /// </param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static T HandleExceptionAndCondition<T, E>(
            Func<T> f,
            Func<T, bool> shouldRetry,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            return HandleExceptionAndCondition<T, E>(f, shouldRetry, e => true, retries,
                retryDelayInSeconds);
        }

        /// <summary>
        /// Retry the function if an exception is thrown or the desired result in not reached.
        /// shouldRetryException indicates if the result should be retried.
        /// shouldRetryException indicates if the exception is able to be retried.
        /// E is defined as the Exception to handle.
        /// </summary>
        /// <param name="f">Function to attempt.</param>
        /// <param name="shouldRetry">
        /// Function that determines if function f should be retried.
        /// If false the result is returned.
        /// </param>
        /// <param name="shouldRetryException">
        /// Function that determines if function f should be retried when an exception is
        /// thrown. If false the exception is thrown.
        /// </param>
        /// <param name="retries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayInSeconds">Seconds delayed between retries.</param>
        /// <returns>The result of calling the f function.</returns>
        /// <exception cref="AggregateException">
        /// When all retry attempts are exhausted.
        /// Exception will contain the aggregate of all exceptions thrown.
        /// </exception>
        /// <exception cref="Exception">
        /// Will propagate the original exception if exception is not retried.
        /// </exception>
        public static T HandleExceptionAndCondition<T, E>(
            Func<T> f,
            Func<T, bool> shouldRetry,
            Func<E, bool> shouldRetryException,
            int retries = 5,
            double retryDelayInSeconds = 5) where E : Exception
        {
            CheckParams(f, shouldRetry, shouldRetryException);

            int retriesRemaining = retries;
            var exceptions = new List<Exception>();
            var result = default(T);
            bool hasResult = false;
            do
            {
                try
                {
                    result = f.Invoke();
                    hasResult = true;
                    if (!shouldRetry(result)) return result;
                }
                catch (E e)
                {
                    if (!shouldRetryException(e)) throw;
                    exceptions.Insert(0, e);
                }

                if (retryDelayInSeconds > 0)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(retryDelayInSeconds));
                }
            } while (--retriesRemaining > 0);

            if (hasResult) return result;
            throw new AggregateException(exceptions);
        }

        #region Param Validation Helpers

        private static void CheckParams<T>(Func<T> f) => CheckFParam(f);

        private static void CheckParams<T, E>(Func<T> f, Func<E, bool> shouldRetryException)
        {
            CheckFParam(f);
            CheckShouldRetryExceptionParam(shouldRetryException);
        }

        private static void CheckParams<E>(Action f, Func<E, bool> shouldRetryException)
        {
            CheckFParam(f);
            CheckShouldRetryExceptionParam(shouldRetryException);
        }

        private static void CheckParams<T, E>(
            Func<Task<T>> f,
            Func<T, bool> shouldRetry,
            Func<E, bool> shouldRetryException)
        {
            CheckFParam(f);
            CheckShouldRetryParam(shouldRetry);
            CheckShouldRetryExceptionParam(shouldRetryException);
        }

        private static void CheckParams<T, E>(
            Func<T> f,
            Func<T, bool> shouldRetry,
            Func<E, bool> shouldRetryException)
        {
            CheckFParam(f);
            CheckShouldRetryParam(shouldRetry);
            CheckShouldRetryExceptionParam(shouldRetryException);
        }

        private static void CheckFParam<T>(Func<T> f)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
        }

        private static void CheckFParam(Action f)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
        }

        private static void CheckShouldRetryParam<T>(Func<T, bool> shouldRetry)
        {
            if (shouldRetry == null) throw new ArgumentNullException(nameof(shouldRetry));
        }

        private static void CheckShouldRetryExceptionParam<E>(Func<E, bool> shouldRetryException)
        {
            if (shouldRetryException == null) throw new ArgumentNullException(nameof(shouldRetryException));
        }

        #endregion
    }
}
