// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.DecisionService.Common.Trainer
{
    public class ReceiveException : Exception
    {
        public ReceiveException(Exception ex) : base(ex.Message, ex)
        {
        }
    }
}
