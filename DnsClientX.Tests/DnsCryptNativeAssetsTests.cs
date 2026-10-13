using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DnsClientX.DnsCrypt;

namespace DnsClientX.Tests;

/// <summary>Bundled Windows native assets retain their deployment location under Framework shadow copying.</summary>
public class DnsCryptNativeAssetsTests {
    /// <summary>The original file URI takes precedence over a managed shadow-cache location.</summary>
    [Theory]
    [InlineData(Architecture.X86, "win-x86")]
    [InlineData(Architecture.X64, "win-x64")]
    [InlineData(Architecture.Arm64, "win-arm64")]
    public void OriginalAssemblyDirectoryPrecedesShadowCopyAndApplicationDirectory(Architecture architecture, string rid) {
        string original = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "package origin % space");
        string shadow = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assembly", "dl3", "shadow");
        string codeBase = new Uri(Path.Combine(original, "DnsClientX.DnsCrypt.dll")).AbsoluteUri;

        string[] paths = DnsCryptSodium.FrameworkNativeLibraryPaths(AppDomain.CurrentDomain.BaseDirectory,
            codeBase, Path.Combine(shadow, "DnsClientX.DnsCrypt.dll"), architecture).ToArray();

        Assert.Equal(Path.Combine(original, "runtimes", rid, "native", "libsodium.dll"), paths[0]);
        Assert.Equal(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", rid, "native", "libsodium.dll"), paths[1]);
        Assert.Equal(Path.Combine(shadow, "runtimes", rid, "native", "libsodium.dll"), paths[2]);
    }

    /// <summary>Non-file origins and relative directories cannot introduce ambient native search paths.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("https://example.com/DnsClientX.DnsCrypt.dll")]
    public void ApplicationDirectorySupportsHostsWithoutAFileCodeBase(string? codeBase) {
        string[] paths = DnsCryptSodium.FrameworkNativeLibraryPaths(AppDomain.CurrentDomain.BaseDirectory,
            codeBase, "DnsClientX.DnsCrypt.dll", Architecture.X64).ToArray();

        Assert.Equal(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", "win-x64", "native", "libsodium.dll"), Assert.Single(paths));
    }

    /// <summary>Ordinary deployment yields one canonical path when all managed locations agree.</summary>
    [Fact]
    public void DuplicateDeploymentDirectoriesAreResolvedOnce() {
        string assembly = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DnsClientX.DnsCrypt.dll");
        Assert.Single(DnsCryptSodium.FrameworkNativeLibraryPaths(AppDomain.CurrentDomain.BaseDirectory,
            new Uri(assembly).AbsoluteUri, assembly, Architecture.X64));
    }

    /// <summary>An architecture without a packaged DLL cannot select a different process architecture.</summary>
    [Fact]
    public void UnsupportedArchitectureFailsClosed() {
        Assert.Throws<PlatformNotSupportedException>(() => DnsCryptSodium.FrameworkNativeLibraryPaths(
            AppDomain.CurrentDomain.BaseDirectory, null, string.Empty, Architecture.Arm).ToArray());
    }

#if NETFRAMEWORK
    /// <summary>The actual Framework host resolves its bundled DLL even when its provider assembly was shadow copied.</summary>
    [Fact]
    public void FrameworkHostLoadsBundledNativeAssetFromOriginalDeployment() {
        var assembly = typeof(DnsCryptClient).Assembly;
        string[] paths = DnsCryptSodium.FrameworkNativeLibraryPaths(AppDomain.CurrentDomain.BaseDirectory,
            assembly.CodeBase, assembly.Location, RuntimeInformation.ProcessArchitecture).ToArray();
        Assert.Contains(paths, File.Exists);
        Assert.Equal("1.0.22", DnsCryptClient.NativeLibraryVersion);
    }
#endif
}
