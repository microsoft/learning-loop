// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common;

public class KeyVaultConfig
{
    /// <summary>
    /// The vault to use for getting secrets from. The value can be either just the vault name,
    /// for example <c>myvault</c> or it can be a fully qualified URI such as
    /// <c>https://myvault.vault.azure.net/</c>.
    /// </summary>
    public string? KeyVault { get; set; } = null;

    public TimeSpan? KeyVaultReloadInterval { get; set; } = null;
}