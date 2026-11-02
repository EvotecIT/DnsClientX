using System;

namespace DnsClientX;

/// <summary>Loads an explicitly selected optional DNSSEC provider without adding it to core dependencies.</summary>
public static class DnsSecSignatureVerifierLoader {
    /// <summary>Creates the assembly's single public, parameterless DNSSEC signature verifier.</summary>
    /// <param name="assemblyPath">Local provider assembly path. Its dependencies must be available beside it.</param>
    /// <returns>A thread-safe verifier implemented by the selected assembly.</returns>
    /// <remarks>Loading an assembly executes local code. Select only a provider trusted by the application.</remarks>
    public static IDnsSecSignatureVerifier Load(string assemblyPath) {
        return OptionalDnsProviderLoader.Load<IDnsSecSignatureVerifier>(assemblyPath, "DNSSEC");
    }
}
