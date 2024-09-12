// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace Microsoft.VW.VWBinResolver;

public enum OperatingSystem
{
    Linux,
    MacOs,
    Windows,
    FreeBsd
}

public enum ProcessorArchitecture
{
    X64,
    X86,
    Arm,
    Arm64
}

static class RuntimePlatformInfo
{
    public static ProcessorArchitecture GetProcessorArchitecture()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        return arch switch
        {
            Architecture.X64 => ProcessorArchitecture.X64,
            Architecture.X86 => ProcessorArchitecture.X86,
            Architecture.Arm => ProcessorArchitecture.Arm,
            Architecture.Arm64 => ProcessorArchitecture.Arm64,
            _ => throw new PlatformNotSupportedException()
        };
    }

    public static OperatingSystem GetOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OperatingSystem.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OperatingSystem.MacOs;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OperatingSystem.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            return OperatingSystem.FreeBsd;
        }

        throw new PlatformNotSupportedException();
    }


    public static string GetString(this OperatingSystem platform)
    {
        return platform switch
        {
            OperatingSystem.Linux => "linux",
            OperatingSystem.MacOs => "macos",
            OperatingSystem.Windows => "win",
            OperatingSystem.FreeBsd => "freebsd",
            _ => throw new PlatformNotSupportedException()
        };
    }

    public static string GetExtension(this OperatingSystem platform)
    {
        return platform switch
        {
            OperatingSystem.Linux => "",
            OperatingSystem.MacOs => "",
            OperatingSystem.Windows => ".exe",
            OperatingSystem.FreeBsd => "",
            _ => throw new PlatformNotSupportedException()
        };
    }

    public static string GetString(this ProcessorArchitecture architecture)
    {
        return architecture switch
        {
            ProcessorArchitecture.X64 => "x64",
            ProcessorArchitecture.X86 => "x86",
            ProcessorArchitecture.Arm => "arm",
            ProcessorArchitecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException()
        };
    }
}

public static class Resolver
{
    public static string ResolveVwBinary(
        ProcessorArchitecture? inProcessorArchitecture = null,
        OperatingSystem? inOperatingSystem = null
    )
    {
        var processorArchitecture = inProcessorArchitecture ?? RuntimePlatformInfo.GetProcessorArchitecture();
        var operatingSystem = inOperatingSystem ?? RuntimePlatformInfo.GetOperatingSystem();
            
        var baseDir = AppContext.BaseDirectory;
        var binName = $"vw-{operatingSystem.GetString()}-{processorArchitecture.GetString()}{operatingSystem.GetExtension()}";
        var fullPath = Path.Join(baseDir, "vw-bin", binName);
        
        // Check if the path exists
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Could not find VW binary at {fullPath}");
        }

        return fullPath;
    }
}