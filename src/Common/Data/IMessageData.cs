// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Data
{
    public interface IMessageData
    {
        string StreamId { get; }
        long StreamOffset { get; }
        long StreamSequenceNumber { get; }
        DateTime EnqueuedTimeUtc { get; }
        ArraySegment<byte> Bytes { get; }
    }
}