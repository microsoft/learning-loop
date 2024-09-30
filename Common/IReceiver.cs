// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.DecisionService.Common.Data;

namespace Microsoft.DecisionService.Common.Trainer
{
    public interface IReceiver
    {
        Task<bool> IsHistoricalModeAsync(DateTime? warmstartStartDateTime);
        Task CloseAsync();
        Task<IMessageData[]> ReceiveAsync(TimeSpan receiveTimeout);
    }
}
