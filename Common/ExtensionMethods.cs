// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DecisionService.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Microsoft.DecisionService.Common
{
    public static class ExtensionMethods
    {

        public static Task CancelOnFaultedAsync(this Task task, CancellationTokenSource cts)
        {
            var _ = task.ContinueWith(_ => cts.Cancel(), cts.Token, TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            return task;
        }

        public static Task<T> CancelOnFaultedAsync<T>(this Task<T> task, CancellationTokenSource cts)
        {
            var _ = task.ContinueWith(_ => cts.Cancel(), cts.Token, TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            return task;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks",
            Justification = "Method is a Task extension")]
        public static Task TraceAsync(
            this Task task,
            ILogger logger,
            string message,
            string? eventKey = null,
            Dictionary<string, string>? properties = null,
            string? errorCode = null,
            TracingLevel tracingLevel = TracingLevel.Informational,
            [CallerMemberName] string? memberName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            if (logger == null)
                return task;

            return task.ContinueWith(beforeTask =>
            {
                // forward original log location
                logger.Log(tracingLevel.ToLogLevel(),
                    "{Message} TaskStatusOnExit:{Status} {EventKey} {Properties} {MemberName} {FilePath}, {LineNumber}",
                    message, beforeTask?.Status, eventKey, properties, memberName, filePath, lineNumber);

                if (beforeTask != null && beforeTask.IsFaulted && beforeTask.Exception != null)
                {
                    logger.Log(tracingLevel.ToLogLevel(), beforeTask.Exception,
                        "{EventKey} {Properties} {ErrorCode} {MemberName} {FilePath}, {LineNumber}", eventKey,
                        properties, errorCode, memberName, filePath, lineNumber);
                    throw new AggregateException(
                        $"Rethrowing exception in ContinueWith. Original message: {beforeTask.Exception.Message}",
                        beforeTask.Exception);
                }
            }, TaskScheduler.Default);
        }

        public static Task ContinueWithPropagateCancellationAsync(this Task<Task> task, CancellationTokenSource cts,
            ILogger logger, TracingLevel tracingLevel = TracingLevel.Informational,
            [CallerFilePath] string filePath = null)
        {
            string className = Path.GetFileNameWithoutExtension(filePath);
            return task.ContinueWith(t =>
            {
                if (cts == null)
                {
                    logger?.Log(tracingLevel.ToLogLevel(), "CancellationTokenSource is null {EventKey}, {FilePath}",
                        $"{className}.OnExit",
                        filePath);
                }
                else if (cts.Token.IsCancellationRequested)
                {
                    logger?.Log(tracingLevel.ToLogLevel(), "Cancellation requested {EventKey}, {FilePath}",
                        $"{className}.OnExit",
                        filePath);
                }
                else
                {
                    var aggregateException = t.Result?.Exception as AggregateException;
                    if (aggregateException != null)
                    {
                        foreach (var innerException in aggregateException.Flatten().InnerExceptions)
                        {
                            logger?.Log(tracingLevel.ToLogLevel(), innerException,
                                "StackTrace: {StackTrace} {EventKey}, {FilePath}",
                                innerException.StackTrace, $"{className}.OnExit", filePath);
                        }
                    }
                    else if (t.Result?.Exception != null)
                    {
                        var exception = t.Result.Exception;
                        logger?.Log(tracingLevel.ToLogLevel(), exception,
                            "StackTrace: {StackTrace} {EventKey}, {FilePath}",
                            exception.StackTrace, $"{className}.OnExit", filePath);
                    }

                    cts.Cancel();
                }
            }, TaskScheduler.Default);
        }

        public static (List<T>, List<T>) PartitionByPredicate<T>(this List<T> task, Predicate<T> predicate)
        {
            var trueList = new List<T>();
            var falseList = new List<T>();
            foreach (var item in task)
            {
                if (predicate(item))
                {
                    trueList.Add(item);
                }
                else
                {
                    falseList.Add(item);
                }
            }

            return (trueList, falseList);
            
        }

    }
}
