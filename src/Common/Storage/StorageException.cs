// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Storage
{
    public class StorageException : Exception
    {
        public StorageException()
        {
        }

        public StorageException(string message) : base(message)
        {
        }

        public StorageException(string message, Exception innerException) : base(message, innerException)
        {
        }
        public StorageException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        public StorageException(string message, string? errorCode, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public string? ErrorCode { get; private set; }
    }
}
