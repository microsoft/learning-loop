// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.DecisionService.Common.Trainer;

public class StorageConfig
{
    [Required]
    public string AppId { get; set; }

    [Required]
    public Uri? StorageAccountUrl { get; set; } = null;
}