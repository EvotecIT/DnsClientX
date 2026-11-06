using System;

namespace DnsClientX;

/// <summary>Loads a trusted, explicitly selected optional DNS transport without adding crypto dependencies to the core.</summary>
public static class DnsWireTransportProviderLoader {
    /// <summary>Loads the assembly's single transport factory and creates a caller-owned provider.</summary>
    /// <param name="assemblyPath">Local trusted DLL path, with dependencies deployed beside it.</param>
    /// <param name="resolver">Trusted DNSCrypt stamp or ODoH target stamp/HTTPS endpoint.</param>
    /// <param name="relay">Optional DNSCrypt relay stamp; required ODoH relay stamp/HTTPS endpoint.</param>
    /// <returns>A provider the caller must dispose after its clients finish.</returns>
    /// <remarks>Loading a local assembly executes code. Only load providers trusted by the application.</remarks>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Provider assembly discovery requires preserved provider types. Register a provider instance directly in trimmed applications.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Loading external managed provider assemblies is unavailable to Native AOT. Register a statically referenced provider instance directly.")]
#endif
    public static IDnsWireTransportProvider Load(string assemblyPath, string resolver, string? relay = null) {
        return OptionalDnsProviderLoader.Load<IDnsWireTransportFactory>(assemblyPath, "wire transport").Create(resolver, relay)
            ?? throw new InvalidOperationException("The selected privacy provider did not create a transport.");
    }
}
