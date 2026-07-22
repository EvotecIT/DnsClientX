using System;

namespace DnsClientX.DnsSec.EdDsa;

/// <summary>Configures optional RFC 8080 signature verification.</summary>
public static class DnsSecEdDsaExtensions {
    /// <summary>Enables Ed25519 and Ed448 DNSSEC validation for this configuration.</summary>
    /// <returns>The same configuration for fluent setup.</returns>
    public static Configuration UseEdDsaDnsSec(this Configuration configuration) {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        configuration.DnsSecSignatureVerifier = new EdDsaDnsSecSignatureVerifier();
        return configuration;
    }
}
