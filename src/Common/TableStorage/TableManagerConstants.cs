// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.TableStorage
{
    public static class TableManagerConstants
    {
        public const string ConfigurationTableName = "Configurations";
        public const string ConfigurationTablePartitionKey = "Configuration";
        public static readonly DateTime MinDateTime = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static readonly DateTime DefaultLastConfigurationEditDate = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public const string VersionPropertyName = "Version";
    }
}
