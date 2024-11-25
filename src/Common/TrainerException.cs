// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.DecisionService.VowpalWabbit;

namespace Microsoft.DecisionService.Common.Trainer
{
    /// <summary>
    /// Thrown when there is an error in the trainer.
    /// </summary>
    [Serializable]
    public class TrainerException : Exception
    {
        public TrainerException() { }
        public TrainerException(string message) : base(message) { }
        public TrainerException(string message, Exception inner) : base(message, inner) { }
        
        public static TrainerException CreateFromVwOutput(string extraMessage, VwOutput vwOutput)
        {
            // TODO make this more useful
            return new TrainerException($"{extraMessage} {vwOutput.LogLines}");
        }
        
    }
}
