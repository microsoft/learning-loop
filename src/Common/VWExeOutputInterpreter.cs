// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DecisionService.Common.Utils.Exe;
using Microsoft.DecisionService.Instrumentation;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.DecisionService.VowpalWabbit
{
    // VW, by default, outputs logs to stdout and driver output to stderr.
    // We can check the arguments to determine where things went if the user specified a different output location.
    // We are going to throw an error if both driver and logs are defined to go to the same location, or if the user uses "compat" mode which outputs to both output streams in various locations.

    public class LineInfo
    {
        public TracingLevel TracingLevel;
        public string Message;
    }
    
    public class VwOutput
    {
        public List<LineInfo> LogLines  { get; set; }
        public string DriverOutput { get; set; }
    }
    
    public static class VWExeOutputInterpreter
    {
        private enum OutputLocation
        {
            Stderr,
            Stdout,
            Compat
        }
    
        // This is not fool proof since we are doing a best effort at interpreting a command line string
        private static OutputLocation GetDriverOutputLocation(string args)
        {
            if (args.Contains("--driver_output=stderr") || args.Contains("--driver_output stderr"))
            {
                return OutputLocation.Stderr;
            }
            else if (args.Contains("--driver_output=stdout") || args.Contains("--driver_output stdout"))
            {
                return OutputLocation.Stdout;
            }
            else
            {
                return OutputLocation.Stderr;
            }
        }
        
        private static OutputLocation GetLogOutputLocation(string args)
        {
            if (args.Contains("--log_output=stderr") || args.Contains("--log_output stderr"))
            {
                return OutputLocation.Stderr;
            }
            else if (args.Contains("--log_output=stdout") || args.Contains("--log_output stdout"))
            {
                return OutputLocation.Stdout;
            }
            else if (args.Contains("--log_output=compat") || args.Contains("--log_output compat"))
            {
                return OutputLocation.Compat;
            }
            else
            {
                return OutputLocation.Stdout;
            }
        }
        
        private static readonly Regex LineInfoRegex = new Regex(@"^\[(\w+)\] (.*)$", RegexOptions.IgnoreCase);
        
        public static VwOutput InterpretOutput(RunResult runResult)
        {
            // Sanity checks on the ability to interpret the output.
            var driverOutputLocation = GetDriverOutputLocation(runResult.Arguments);
            var logOutputLocation = GetLogOutputLocation(runResult.Arguments);
            if (logOutputLocation == OutputLocation.Compat)
            {
                throw new ArgumentException("Cannot interpret output when using --log_output=compat");
            }
            
            if (driverOutputLocation == logOutputLocation)
            {
                throw new ArgumentException("Cannot interpret output when driver and log output are the same");
            }
            
            var driverOutput = driverOutputLocation == OutputLocation.Stderr ? runResult.Error : runResult.Output;
            
            // It is possible for a log line to contain a newline, so we actually need to split the logs based on if a line starts with 
            // [info], [warning], [error] or [critical].
            // So we will still split by newline, but if a line does not begin with one of those, we will assume it is part of the previous line.
            var logLines = new List<LineInfo>();
            
            var logOutput = logOutputLocation == OutputLocation.Stderr ? runResult.Error : runResult.Output;
            var lines = logOutput.Split(Environment.NewLine);
            var currentLine = "";
            foreach (var line in lines)
            {
                if (line.StartsWith("[info]") || line.StartsWith("[warning]") || line.StartsWith("[error]") || line.StartsWith("[critical]"))
                {
                    if (currentLine != "")
                    {
                        logLines.Add(GetLineInfo(currentLine));
                    }
                    currentLine = line;
                }
                else
                {
                    currentLine += line;
                }
            }
            logLines.Add(GetLineInfo(currentLine));

            return new VwOutput
            {
                LogLines = logLines,
                DriverOutput = driverOutput
            };
        }

        private static LineInfo GetLineInfo(string line)
        {
            Match match = LineInfoRegex.Match(line);
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
    }
}
