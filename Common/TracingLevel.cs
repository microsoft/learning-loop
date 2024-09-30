// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.DecisionService.Instrumentation
{
    public enum TracingLevel
    {
        Critical = 1,
        Error = 2,
        Warning = 3,
        Informational = 4,
        Verbose = 5
    }

    public static class TracingLevelExtensions
    {
        public static LogLevel ToLogLevel(this TracingLevel tracingLevel)
        {
            switch (tracingLevel)
            {
                case TracingLevel.Critical:
                    return LogLevel.Critical;
                case TracingLevel.Error:
                    return LogLevel.Error;
                case TracingLevel.Warning:
                    return LogLevel.Warning;
                case TracingLevel.Informational:
                    return LogLevel.Information;
                case TracingLevel.Verbose:
                    return LogLevel.Debug;
                default:
                    return LogLevel.None;
            }
        }
    }
}
