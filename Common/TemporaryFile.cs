// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

namespace Microsoft.DecisionService.Common;

/// <summary>
/// Creates a temporary file that is deleted when the object is disposed.
/// </summary>
public class TempFile : IDisposable
{
    public string FilePath { get; private set;  }
    public TempFile()
    {
        this.FilePath = Path.GetTempFileName();
    }

    private void ReleaseUnmanagedResources()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~TempFile()
    {
        ReleaseUnmanagedResources();
    }
}