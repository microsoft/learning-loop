// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Tests.E2ETools
{
    [Flags]
    public enum E2ETestFeatures
    {
        None = 0,
        ValidateModel = 1,
        UpdateFrontEndModel = 1 << 1,
        ValidateLogMirroring = 1 << 2,
        ValidateApprenticeMode = 1 << 3,
        ValidateDanglingRewards = 1 << 4,
        ValidateCCBApprenticeMode = 1 << 5,
        UseMultitenantBackend = 1 << 6,
        ReuseFrontEnd = 1 << 7,
        OnlyCookedLog = 1 << 8,
        SkipCheckModelLastEventId = 1 << 9,
        ValidateMultiStep = 1 << 10
    }
}
