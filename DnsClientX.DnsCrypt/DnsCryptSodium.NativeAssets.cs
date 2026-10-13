using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DnsClientX.DnsCrypt;

internal static partial class DnsCryptSodium {
    private static void LoadFrameworkNativeLibrary() {
#if NETFRAMEWORK || NETSTANDARD2_0
        // Location points into the CLR shadow cache under Framework hosts. CodeBase
        // retains the assembly's original file URI, alongside its bundled assets.
        var assembly = typeof(DnsCryptSodium).Assembly;
        IEnumerable<string> paths = FrameworkNativeLibraryPaths(AppDomain.CurrentDomain.BaseDirectory,
            assembly.CodeBase, assembly.Location, RuntimeInformation.ProcessArchitecture);
        foreach (string path in paths) {
            if (!File.Exists(path)) continue;
            if (LoadLibraryEx(path, IntPtr.Zero, 0x1100) != IntPtr.Zero) return;
            // A present but unloadable asset is a deployment error. Do not select a
            // different copy, or add the current directory to the DLL search path.
            throw new DllNotFoundException("The packaged libsodium native library could not be loaded from " + path
                + " (Windows error " + Marshal.GetLastWin32Error() + ").");
        }
        throw new DllNotFoundException("The packaged libsodium native library was not found beside the original provider assembly or in the application directory.");
#else
        throw new PlatformNotSupportedException("The .NET Framework native loader requires a compatible provider assembly.");
#endif
    }

    internal static IEnumerable<string> FrameworkNativeLibraryPaths(string applicationDirectory,
        string? assemblyCodeBase, string assemblyLocation, Architecture architecture) {
        string rid;
        switch (architecture) {
            case Architecture.X86: rid = "win-x86"; break;
            case Architecture.X64: rid = "win-x64"; break;
            case Architecture.Arm64: rid = "win-arm64"; break;
            default: throw new PlatformNotSupportedException("DNSCrypt has no packaged Windows native asset for " + architecture + ".");
        }
        var directories = new List<string>();
        if (Uri.TryCreate(assemblyCodeBase, UriKind.Absolute, out Uri? origin) && origin.IsFile) {
            AddDirectory(directories, Path.GetDirectoryName(origin.LocalPath));
        }
        AddDirectory(directories, applicationDirectory);
        if (!string.IsNullOrEmpty(assemblyLocation)) AddDirectory(directories, Path.GetDirectoryName(assemblyLocation));
        foreach (string directory in directories) yield return Path.Combine(directory, "runtimes", rid, "native", "libsodium.dll");
    }

    private static void AddDirectory(List<string> directories, string? directory) {
        if (string.IsNullOrEmpty(directory) || !Path.IsPathRooted(directory)) return;
        string fullPath = Path.GetFullPath(directory);
        string root = Path.GetPathRoot(fullPath)!;
        if (fullPath.Length > root.Length) fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!directories.Exists(existing => string.Equals(existing, fullPath, StringComparison.OrdinalIgnoreCase))) directories.Add(fullPath);
    }
}
