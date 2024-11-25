// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.Serialization;

namespace Microsoft.DecisionService.Common
{
    /// <summary>
    /// Thrown when an invalid <see cref="ModelCheckpoint"/> is detected.
    /// </summary>
    internal class InvalidModelCheckpointException : Exception
    {
        /// <inheritdoc/>
        public InvalidModelCheckpointException()
        {
        }

        public InvalidModelCheckpointException(string message) : base(message)
        {
        }

        /// <inheritdoc/>
        public InvalidModelCheckpointException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <inheritdoc/>
        protected InvalidModelCheckpointException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}