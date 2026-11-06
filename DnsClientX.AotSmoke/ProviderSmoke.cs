using System.Net;
using DnsClientX;
using DnsClientX.DnsCrypt;
using DnsClientX.DnsSec.EdDsa;
using DnsClientX.Odoh;

internal static class ProviderSmoke {
    internal static void Run() {
        // RFC 8032 section 7.1, the Ed25519 empty-message signature.
        byte[] publicKey = Convert.FromHexString("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a");
        byte[] signature = Convert.FromHexString(
            "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155" +
            "5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b");
        var verifier = new EdDsaDnsSecSignatureVerifier();
        if (!verifier.Verify(DnsKeyAlgorithm.ED25519, publicKey, Array.Empty<byte>(), signature)) {
            throw new InvalidOperationException("The native DNSSEC provider rejected the published signature.");
        }
        signature[0] ^= 1;
        if (verifier.Verify(DnsKeyAlgorithm.ED25519, publicKey, Array.Empty<byte>(), signature)) {
            throw new InvalidOperationException("The native DNSSEC provider accepted a modified signature.");
        }

        // The existing independent odoh-go fixture used by the protocol tests.
        OdohTargetConfiguration configuration = OdohTargetConfiguration.Parse(Convert.FromHexString(
            "002c000100280020000100010020ae3bf1cd87c2d2ed25af4a1a239eed04a990f00e7403e4c8065927de010fd17a"),
            DateTimeOffset.UtcNow.AddHours(1));
        if (Convert.ToHexString(configuration.KeyId).ToLowerInvariant() !=
            "e2acc3540a58c7eeac78fef62880c0f4deb0a4d64addc85b1e353d020e907006") {
            throw new InvalidOperationException("The native ODoH configuration produced an incorrect key identifier.");
        }
        using var odoh = new OdohClient(new OdohClientOptions(
            new Uri("https://relay.example/proxy"), new Uri("https://target.example/dns-query")));
        if (!ReferenceEquals(odoh.CreateConfiguration().WireTransport, odoh)) {
            throw new InvalidOperationException("The native ODoH provider lost its configuration binding.");
        }
        using var dnscrypt = new DnsCryptClient(new DnsCryptOptions(
            new IPEndPoint(IPAddress.Loopback, 443), "2.dnscrypt-cert.example", publicKey));
        if (string.IsNullOrWhiteSpace(DnsCryptClient.NativeLibraryVersion) ||
            !ReferenceEquals(dnscrypt.CreateConfiguration().WireTransport, dnscrypt)) {
            throw new InvalidOperationException("The native DNSCrypt provider or its libsodium asset is unavailable.");
        }
    }
}
