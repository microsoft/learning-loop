// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Common.Utils.Exe
{
    public class RunResult {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public string Arguments { get; set; }
        public string Binary { get; set; }
    }

    /// <summary>
    /// Runs an executable and can notify consumer of output and error data.
    /// </summary>
    public interface IExeRunner
    {
        /// <summary>
        /// Runs the exe with the given command line arguments.
        /// </summary>
        /// <param name="arguments">The command line arguments.</param>
        /// <param name="input">Additional input into the exe process.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="timeoutMs">Optional timeout in milliseconds, -1 means indefinite timeout</param>
        /// <returns>The exit code of the process.</returns>
        public Task<RunResult> RunAsync(
            string? arguments = null,
            byte[]? input = null,
            int timeoutMs = -1,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(() => Run(arguments, input, timeoutMs), cancellationToken);
        }

        /// <summary>
        /// Runs the exe with the given command line arguments.
        /// </summary>
        /// <param name="arguments">The command line arguments.</param>
        /// <param name="input">Additional input into the exe process.</param>
        /// <param name="timeoutMs">Optional timeout in milliseconds, -1 means indefinite timeout</param>
        /// <returns>The exit code of the process.</returns>
        public RunResult Run(
            string? arguments = null,
            byte[]? input = null,
            int timeoutMs = -1);
    }
}
