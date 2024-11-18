// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.TableStorage;
using System;

namespace Microsoft.DecisionService.Common.Config
{
    public sealed class TableStorageConfig
    {
        public Uri? TableStorageEndpoint { get; set; } = null;
        public string TableName { get; set; } = TableManagerConstants.ConfigurationTableName;
        public string PartitionKey { get; set; } = TableManagerConstants.ConfigurationTablePartitionKey;
        // Row key is the app id
        public TimeSpan? TableStorageReloadInterval { get; set; } = null;
    }
}
