// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Utils.Exe;
using Microsoft.DecisionService.Instrumentation;
using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;

namespace Microsoft.DecisionService.VowpalWabbit
{
    public sealed class VWExeOutputHandler : IExeRunnerOutputHandler
    {
        private const string EVENT_KEY = "VW.Output";
        private static readonly Regex lineInfoRegex = new Regex(@"^\[(\w+)\] (.*)$", RegexOptions.IgnoreCase);
        private readonly ILogger logger;

        private VWExeOutputHandler(ILogger appIdLogger)
        {
            this.logger = appIdLogger ?? throw new ArgumentNullException(nameof(appIdLogger));
        }

        public static VWExeOutputHandler Create(ILogger logger)
        {
            return logger == null ? null : new VWExeOutputHandler(logger);
        }

        public void HandleErrorLine(string errorLine)
        {
            HandleLine(errorLine);
        }

        public void HandleOutputLine(string outputLine)
        {
            HandleLine(outputLine);
        }

        private static LineInfo GetLineInfo(string line)
        {
            Match match = lineInfoRegex.Match(line);
            if (match.Success)
            {
                return new LineInfo
                {
                    TracingLevel = ParseTraceLevel(match.Groups[1].Value),
                    Message = match.Groups[2].Value,
                };
            }
            else
            {
                return new LineInfo
                {
                    TracingLevel = TracingLevel.Verbose,
                    Message = line
                };
            }
        }

        private static TracingLevel ParseTraceLevel(string level)
        {
            switch (level.ToLower())
            {
                case "info":
                    return TracingLevel.Informational;
                case "warning":
                    return TracingLevel.Warning;
                case "error":
                    return TracingLevel.Error;
                case "critical":
                    return TracingLevel.Critical;
                default:
                    return TracingLevel.Verbose;
            }
        }

        private void HandleLine(string line)
        {
            if (line == null)
            {
                return;
            }

            LineInfo lineInfo = GetLineInfo(line);
            this.logger.Log(lineInfo.TracingLevel.ToLogLevel(), lineInfo.Message);
        }

        private class LineInfo
        {
            public TracingLevel TracingLevel;
            public string Message;
        }
    }
}
