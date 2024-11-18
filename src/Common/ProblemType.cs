// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common
{
    [Flags]
    public enum ProblemType : long
    {
        UNKNOWN = 0,
        CB = 1,
        CCB = 1 << 1,
        SLATES = 1 << 2,
        CA = 1 << 3,
        MULTI_STEP = 1 << 4,
        LARGE_ACTION_SPACE = 1 << 5
    }

    public static class ProblemTypeString
    {
        public const string MultiStep = "MultiStep";
        public const string CB = "CB";
        public const string CCB = "CCB";
    }
}
