// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.DecisionService.Common.Utils.Exe
{
    /// <summary>
    /// Handles output from an <see cref="IExeRunner"/> during execution.
    /// </summary>
    public interface IExeRunnerOutputHandler
    {
        /// <summary>
        /// Handles normal output from <see cref="IExeRunner"/> during execution.
        /// </summary>
        /// <param name="outputLine">The line of output to handle.</param>
        public void HandleOutputLine(string outputLine);

        /// <summary>
        /// Handles normal output from <see cref="IExeRunner"/> during execution.
        /// </summary>
        /// <param name="errorLine">The line of error info to handle.</param>
        public void HandleErrorLine(string errorLine);
    }
}
