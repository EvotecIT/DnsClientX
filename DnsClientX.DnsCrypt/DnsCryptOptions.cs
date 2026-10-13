using System;
using System.Net;

namespace DnsClientX.DnsCrypt;

/// <summary>Controls the classical DNSCrypt encryption systems accepted from a signed certificate response.</summary>
public enum DnsCryptEncryptionPolicy {
    /// <summary>Prefer XChaCha20; permit deployed XSalsa20 certificates when XChaCha20 is unavailable.</summary>
    PreferXChaCha20,
    /// <summary>Require DNSCrypt's NaCl XChaCha20 construction.</summary>
    RequireXChaCha20,
    /// <summary>Require the deployed XSalsa20 DNSCrypt construction.</summary>
    RequireXSalsa20
}

/// <summary>Chooses the network transport carrying encrypted DNSCrypt packets.</summary>
public enum DnsCryptNetworkTransport {
    /// <summary>Try UDP, then retry with a newly encrypted TCP packet on timeout or authenticated truncation.</summary>
    UdpWithTcpFallback,
    /// <summary>Use a separate TCP connection for each exchange.</summary>
    Tcp
}

/// <summary>Explicit trusted resolver identity and transport policy for a DNSCrypt provider.</summary>
public sealed class DnsCryptOptions {
    /// <summary>Creates options with an IP endpoint, certificate question name, and trusted 32-byte Ed25519 public key.</summary>
    public DnsCryptOptions(IPEndPoint resolverEndpoint, string providerName, byte[] providerPublicKey) {
        ResolverEndpoint = resolverEndpoint ?? throw new ArgumentNullException(nameof(resolverEndpoint));
        ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
        ProviderPublicKey = providerPublicKey == null ? throw new ArgumentNullException(nameof(providerPublicKey)) : (byte[])providerPublicKey.Clone();
    }

    /// <summary>Creates options using the provider key's hexadecimal representation, permitting colon separators.</summary>
    public DnsCryptOptions(string resolverAddress, int port, string providerName, string providerPublicKeyHex)
        : this(new IPEndPoint(IPAddress.Parse(resolverAddress), port), providerName, ParseKey(providerPublicKeyHex)) { }

    /// <summary>Gets the resolver's IP address and DNSCrypt port. No hostname bootstrap is performed.</summary>
    public IPEndPoint ResolverEndpoint { get; }
    /// <summary>Gets the certificate TXT question name, conventionally beginning with 2.dnscrypt-cert.</summary>
    public string ProviderName { get; }
    /// <summary>Gets the trusted Ed25519 provider public key. The provider snapshots this array during construction.</summary>
    public byte[] ProviderPublicKey { get; }
    /// <summary>Gets or sets an optional Anonymized DNSCrypt relay endpoint. Discovery and encrypted queries both use it.</summary>
    public IPEndPoint? RelayEndpoint { get; set; }
    /// <summary>Gets or sets the underlying transport, defaulting to UDP with encrypted TCP retry.</summary>
    public DnsCryptNetworkTransport NetworkTransport { get; set; }
    /// <summary>Gets or sets the accepted classical encryption systems.</summary>
    public DnsCryptEncryptionPolicy EncryptionPolicy { get; set; }
    /// <summary>Gets or sets the timeout for each certificate or encrypted exchange.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Gets or sets the periodic certificate refresh interval, capped at one hour.</summary>
    public TimeSpan CertificateRefreshInterval { get; set; } = TimeSpan.FromHours(1);
    /// <summary>Gets or sets the target encrypted UDP query length (256 through 4096 bytes).</summary>
    public int UdpQuerySize { get; set; } = 512;

    internal DnsCryptOptions Snapshot() {
        if (ProviderPublicKey.Length != 32) throw new ArgumentException("The provider Ed25519 public key must contain 32 bytes.");
        if (ResolverEndpoint.Port < 1 || RelayEndpoint?.Port == 0) throw new ArgumentException("A DNSCrypt endpoint requires a nonzero port.");
        string name = DnsWireNameCodec.Normalize(ProviderName);
        if (name.Length == 0 || name == ".") throw new ArgumentException("A certificate provider name is required.");
        DnsWireNameCodec.EncodeLabels(name);
        if (!Enum.IsDefined(typeof(DnsCryptNetworkTransport), NetworkTransport) || !Enum.IsDefined(typeof(DnsCryptEncryptionPolicy), EncryptionPolicy)) throw new ArgumentOutOfRangeException(nameof(NetworkTransport));
        if (Timeout <= TimeSpan.Zero || Timeout.TotalMilliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(Timeout));
        if (CertificateRefreshInterval <= TimeSpan.Zero || CertificateRefreshInterval > TimeSpan.FromHours(1)) throw new ArgumentOutOfRangeException(nameof(CertificateRefreshInterval));
        if (UdpQuerySize < 256 || UdpQuerySize > 4096) throw new ArgumentOutOfRangeException(nameof(UdpQuerySize));
        return new DnsCryptOptions(CloneEndpoint(ResolverEndpoint), name, ProviderPublicKey) {
            RelayEndpoint = RelayEndpoint == null ? null : CloneEndpoint(RelayEndpoint), NetworkTransport = NetworkTransport,
            EncryptionPolicy = EncryptionPolicy, Timeout = Timeout, CertificateRefreshInterval = CertificateRefreshInterval, UdpQuerySize = UdpQuerySize
        };
    }

    private static IPEndPoint CloneEndpoint(IPEndPoint endpoint) => new IPEndPoint(
        endpoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? new IPAddress(endpoint.Address.GetAddressBytes(), endpoint.Address.ScopeId) : new IPAddress(endpoint.Address.GetAddressBytes()), endpoint.Port);

    private static byte[] ParseKey(string hex) {
        if (hex == null) throw new ArgumentNullException(nameof(hex));
        hex = hex.Replace(":", "").Replace(" ", "");
        if (hex.Length != 64) throw new ArgumentException("The provider key must contain 64 hexadecimal characters.", nameof(hex));
        var result = new byte[32];
        for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return result;
    }
}
