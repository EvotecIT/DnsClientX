using System;

namespace DnsClientX;

/// <summary>Loads an explicitly selected optional DNSSEC provider without adding it to core dependencies.</summary>
public static class DnsSecSignatureVerifierLoader {
    /// <summary>Creates the assembly's single public, parameterless DNSSEC signature verifier.</summary>
    /// <param name="assemblyPath">Local provider assembly path. Its dependencies must be available beside it.</param>
    /// <returns>A thread-safe verifier implemented by the selected assembly.</returns>
    /// <remarks>Loading an assembly executes local code. Select only a provider trusted by the application.</remarks>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Provider assembly discovery requires preserved provider types. Register a provider instance directly in trimmed applications.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Loading external managed provider assemblies is unavailable to Native AOT. Register a statically referenced provider instance directly.")]
#endif
    public static IDnsSecSignatureVerifier Load(string assemblyPath) {
        return OptionalDnsProviderLoader.Load<IDnsSecSignatureVerifier>(assemblyPath, "DNSSEC");
    }
}
