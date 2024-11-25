// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.CommandLine; 
using System.Threading;

namespace TestExeApp
{
    /// <summary>
    /// A simple command line app used in testing code that launches an exe.
    /// </summary>
    internal static class Program
    {
        public static int Main(string[] args)
        {
            var rootCommand = new RootCommand();
            var sleepOpt = new Option<int>(
                aliases: new string[] { "--sleep", "-s" },
                description: "The duration the process should sleep before exiting.",
                getDefaultValue: () => 0);
            rootCommand.AddOption(sleepOpt);

            var exitOpt = new Option<int>(
                aliases: new string[] { "--exit-code", "-r" },
                description: "The exit code of the process",
                getDefaultValue: () => 0);
            rootCommand.AddOption(exitOpt);

            var outputOpt = new Option<string>(
                aliases: new string[] { "--output", "-o" },
                description: "The output message of the process for stdout.",
                getDefaultValue: () => null);
            rootCommand.AddOption(outputOpt);

            var errorOpt = new Option<string>(
                aliases: new string[] { "--error", "-e" },
                description: "The error message of the process for stderr.",
                getDefaultValue: () => null);
            rootCommand.AddOption(errorOpt);

            var useStdInOpt = new Option<bool>(
                aliases: new string[] { "--use-stdin", "-i" },
                description: "Read input from stdin.",
                getDefaultValue: () => false);
            rootCommand.AddOption(useStdInOpt);

            rootCommand.SetHandler((sleep, exitCode, output, error, useStdIn) =>
            {
                Process(sleep, exitCode, output, error, useStdIn);
            },
            sleepOpt, exitOpt, outputOpt, errorOpt, useStdInOpt);

            return rootCommand.Invoke(args);
        }

        private static void Process(int sleepMs,
                                   int exitCode,
                                   string output,
                                   string error,
                                   bool useStdIn)
        {
            if (sleepMs > 0)
            {
                Thread.Sleep(sleepMs);
            }

            if (output != null)
            {
                Console.WriteLine(output);
            }

            if (error != null)
            {
                Console.Error.WriteLine(error);
            }

            if (useStdIn)
            {
                string s;
                while ((s = Console.ReadLine()) != null)
                {
                    Console.WriteLine(s);
                }
            }

            Environment.Exit(exitCode);
        }
    }
}
