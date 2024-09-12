// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace DotNetCore.Tests.E2ETools
{
    public interface ILogParser
    {
        List<CookedLogLine> ParseLogs(string logs);
    }
}