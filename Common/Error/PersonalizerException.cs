// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.Serialization;

namespace Microsoft.DecisionService.Common.Error
{
    public class PersonalizerException : Exception
    {
        public PersonalizerErrorCode PersonalizerErrorCode { get; } = PersonalizerErrorCode.InternalServerError;

        public PersonalizerException()
        {
        }

        public PersonalizerException(string message) : base(message)
        {
        }

        public PersonalizerException(PersonalizerErrorCode code, string message) : base(message)
        {
            PersonalizerErrorCode = code;
        }

        public PersonalizerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public PersonalizerException(PersonalizerErrorCode code) : base(code.GetDescription())
        {
            PersonalizerErrorCode = code;
        }

        public PersonalizerException(PersonalizerErrorCode code, Exception innerException) : base(code.GetDescription(), innerException)
        {
            PersonalizerErrorCode = code;
        }

        protected PersonalizerException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}