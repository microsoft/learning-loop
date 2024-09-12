// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.DecisionService.Common.Utils.Exe
{
    /// <summary>
    /// An <see cref="IExeRunner"/> suitable for running a command line application.
    /// </summary>
    public class CommandLineExeRunner : IExeRunner
    {
        private readonly string _exeFullPath;

        /// <summary>
        /// Create a <see cref="CommandLineExeRunner"/> that runs the given executable.
        /// </summary>
        /// <param name="fullPathToBinary">
        /// The exact path to the executable to run.
        /// </param>
        /// <exception cref="FileNotFoundException">
        /// When <paramref name="fullPathToBinary"/> does not exist.
        /// </exception>
        public CommandLineExeRunner(string fullPathToBinary)
        {
            if (!File.Exists(fullPathToBinary))
            {
                throw new FileNotFoundException($"Could not find executable {fullPathToBinary}");
            }
            this._exeFullPath = fullPathToBinary;
        }


        /// <inheritdoc />
        public virtual RunResult Run(
            string? arguments = null,
            byte[]? input = null,
            int timeoutMs = -1
            )
        {
            var hasInput = input != null && input.Length > 0;
            using Process proc = CreateProcess(arguments ?? string.Empty, useStdin: hasInput);
            proc.Start();
            if (hasInput)
            {
                proc.StandardInput.Write(Encoding.UTF8.GetChars(input));
                proc.StandardInput.Close();
            }
            // Add a time out just in case
            var didExit = proc.WaitForExit(timeoutMs);

            if (!didExit)
            {
                proc.Kill();
            }

            return new RunResult
            {
                ExitCode = proc.ExitCode,
                Output = proc.StandardOutput.ReadToEnd(),
                Error = proc.StandardError.ReadToEnd(),
                Binary = this._exeFullPath,
                Arguments = arguments ?? string.Empty,
            };
        }

        private Process CreateProcess(
            string arguments,
            bool useStdin)
        {
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = useStdin,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                FileName = this._exeFullPath,
                Arguments = arguments
            };

            return new Process { StartInfo = startInfo };
        }
    }
}