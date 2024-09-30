// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Tests.E2ETools
{
    public interface ILogParser
    {
        List<CookedLogLine> ParseLogs(string logs);
    }
}