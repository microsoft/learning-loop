// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DecisionService.Common.Storage;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Microsoft.DecisionService.Common;

public static partial class LoggingExtensions
{
    [LoggerMessage(0, LogLevel.Information,
        "EventId=`{eventId}`, EventType=`{eventType}`, EventKey=`{eventKey}`, Timestamp=`{timestamp}`, PartitionId=`{partitionId}`, Message=`{message}`")]
    public static partial void LogDataFlow(this ILogger logger, string eventId, string eventType,
        string eventKey, DateTime timestamp,
        string partitionId = null,
        string message = null);
    
    // Only difference is that it logs at Trace level, and so is not included in the default logging
    [LoggerMessage(0, LogLevel.Trace,
        "EventId=`{eventId}`, EventType=`{eventType}`, EventKey=`{eventKey}`, Timestamp=`{timestamp}`, PartitionId=`{partitionId}`, Message=`{message}`")]
    public static partial void LogDataFlowTrace(this ILogger logger, string eventId, string eventType,
        string eventKey, DateTime timestamp,
        string partitionId = null,
        string message = null);

    
    public static void LogStorageException(this ILogger logger, StorageException se, string eventKey, string errorCode,
        IBlobClient blob, StorageUploadType storageType)
    {
        var errorDict = new Dictionary<string, string>()
        {
            { "Blob", blob?.Name },
            { "Source", se.Source },
            { "StorageType", storageType.ToString() },
            { "ErrorCode", se.ErrorCode },
            { "ErrorMessage", se.Message }
        };
        var kvstrs = errorDict.Select(x => $"{x.Key}={x.Value}");
        var msg = string.Join(" | ", kvstrs);
        logger.Log(storageType == StorageUploadType.Mirror
                ? LogLevel.Information
                : LogLevel.Error,
            se,
            "{EventKey} {Message} {StorageType} {ErrorCode}",
            eventKey,
            msg, storageType,
            errorCode
        );
    }
}