// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetCore.Tests.E2ETools
{
    /// <summary>
    /// Request records that should not be included in the cooked logs.
    /// </summary>
    public class ErrorRequestRecord : RequestRecord
    {
        /// <summary>
        /// An expected error record is one that should not be counted when evaluating accuracy.
        /// </summary>
        public bool Expected { get; set; } = false;
    }
}