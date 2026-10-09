using System;
using System.Management.Automation;

namespace DnsClientX.PowerShell {
    internal static class DnsSecProviderPath {
        // Call before the first await, while PowerShell session state belongs to the pipeline thread.
        internal static IDnsSecSignatureVerifier? Load(PSCmdlet cmdlet, string? path) {
            if (path == null) return null;
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("The DNSSEC provider requires a nonblank local DLL path.", nameof(path));
            string absolute = cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path,
                out ProviderInfo provider, out _);
            if (!string.Equals(provider.Name, "FileSystem", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The DNSSEC provider must be a local filesystem DLL.", nameof(path));
            return DnsSecSignatureVerifierLoader.Load(absolute);
        }
    }
}
