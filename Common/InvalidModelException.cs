// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.DecisionService.VowpalWabbit;

namespace Microsoft.DecisionService.Common.Trainer
{
    /// <summary>
    /// Thrown when a model is invalid.
    /// </summary>
    [Serializable]
    public sealed class InvalidModelException : TrainerException
    {
        public InvalidModelException() { }
        public InvalidModelException(string message) : base(message) { }
        public InvalidModelException(string message, Exception inner) : base(message, inner) { }
        
        public new static TrainerException CreateFromVwOutput(string extraMessage, VwOutput vwOutput)
        {
            // TODO make this more useful
            return new TrainerException($"{extraMessage} {vwOutput.LogLines}");
        }
    }
}
