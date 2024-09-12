// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Trainer.Join
{
    public enum PartitionSourceState
    {
        Default,              // Event source default state
        Historical,           // Event source is historical mode.
                              //    When eventhub has large data from the past to load, it usually takes time for EventHub client to be able to read the first message.
                              //    We define this as Historical Mode for EventHub.
        Active,               // Event source is active.
        Inactive,             // Event source is inactive.
        Paused,               // Event source is paused due to errors.
    }
}
