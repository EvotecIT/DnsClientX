using System;
using System.Net;

namespace DnsClientX;

/// <summary>The distinct resolver and relay roles encoded by privacy DNS stamps.</summary>
public enum DnsPrivacyStampKind {
    /// <summary>An authenticated DNSCrypt resolver.</summary>
    DnsCrypt = 0x01,
    /// <summary>An Oblivious DoH target.</summary>
    OdohTarget = 0x05,
    /// <summary>An Anonymized DNSCrypt relay.</summary>
    DnsCryptRelay = 0x81,
    /// <summary>An Oblivious DoH relay.</summary>
    OdohRelay = 0x85
}

/// <summary>Parsed privacy-provider identity. Parsing a stamp does not load a provider or contact a resolver.</summary>
public sealed class DnsPrivacyStamp {
    private readonly byte[] _providerKey;
    private readonly IPEndPoint? _address;
    internal DnsPrivacyStamp(DnsPrivacyStampKind kind, ulong properties, IPEndPoint? address,
        string? providerName, byte[]? providerKey, Uri? endpoint) {
        Kind = kind; Properties = properties; _address = address;
        ProviderName = providerName; _providerKey = providerKey ?? Array.Empty<byte>(); Endpoint = endpoint;
    }
    /// <summary>Gets the resolver or relay role.</summary>
    public DnsPrivacyStampKind Kind { get; }
    /// <summary>Gets the stamp's informational DNSSEC, logging and filtering property bits.</summary>
    public ulong Properties { get; }
    /// <summary>Gets a fresh IP endpoint for a DNSCrypt resolver or relay.</summary>
    public IPEndPoint? Address => _address == null ? null : new IPEndPoint(
        _address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? new IPAddress(_address.Address.GetAddressBytes(), _address.Address.ScopeId)
            : new IPAddress(_address.Address.GetAddressBytes()), _address.Port);
    /// <summary>Gets the DNSCrypt certificate provider name.</summary>
    public string? ProviderName { get; }
    /// <summary>Gets a copy of the trusted DNSCrypt Ed25519 public key.</summary>
    public byte[] ProviderPublicKey => (byte[])_providerKey.Clone();
    /// <summary>Gets the authenticated HTTPS endpoint of an ODoH target or relay.</summary>
    public Uri? Endpoint { get; }
}
