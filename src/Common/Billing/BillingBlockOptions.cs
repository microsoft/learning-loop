// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Trainer
{
    public class BillingBlockOptions
    {
        /// <summary>
        /// This size should be small. Each may bring 100MB memory usage
        /// </summary>
        public int BlockBufferCapacity { get; set; }
        
        public string AppId { get; set; }
    }
}
