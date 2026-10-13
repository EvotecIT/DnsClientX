using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace DnsClientX;
/// <summary>Records whether a designated resolver passed RFC 9462 identity verification.</summary>
public enum DnsDesignatedResolverVerification {
    /// <summary>Discovery was requested without verification; the candidate cannot configure a client automatically.</summary>
    NotAttempted,
    /// <summary>The certificate chain and original resolver's iPAddress SAN passed during a TLS connection.</summary>
    Verified,
    /// <summary>A connection, certificate, or original resolver identity check failed.</summary>
    Failed,
    /// <summary>The advertised transport cannot be verified or executed while retaining the original resolver identity.</summary>
    Unsupported
}

/// <summary>Controls bounded, per-resolver designated-resolver discovery.</summary>
public sealed class DnsDesignatedResolverDiscoveryOptions {
    /// <summary>Gets or sets whether discovery connects to candidates and verifies the original resolver IP. Defaults to true.</summary>
    public bool Verify { get; set; } = true;
    /// <summary>Gets or sets the maximum SVCB/CNAME alias depth.</summary>
    public int MaximumAliasHops { get; set; } = 8;
    /// <summary>Gets or sets the maximum number of advertised transport/address candidates.</summary>
    public int MaximumCandidates { get; set; } = 64;
    /// <summary>Gets or sets the TLS connection and verification timeout per candidate.</summary>
    public TimeSpan VerificationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Gets or sets the ordered ALPN preferences. Null uses h2, HTTP/1.1, DoT, HTTP/3, and DoQ.</summary>
    public IReadOnlyList<string>? SupportedAlpn { get; set; }
    /// <summary>Gets or sets isolated application TLS trust anchors for verification, without writing certificate stores.</summary>
    /// <remarks>Custom trust verification requires modern .NET. A resulting configuration must retain the same roots.</remarks>
    public IReadOnlyList<X509Certificate2>? TrustAnchors { get; set; }
    /// <summary>Gets or sets the explicit certificate revocation policy. The default checks online.</summary>
    public X509RevocationMode RevocationMode { get; set; } = X509RevocationMode.Online;
}

/// <summary>Describes one DDR candidate bound to the original resolver that supplied its designation.</summary>
/// <remarks>Discovery never changes the existing client or installs a global resolver. Candidates expire with their DNS evidence.</remarks>
public sealed class DnsDesignatedResolver {
    private readonly IPAddress _originalAddress;
    private readonly IPAddress _connectionAddress;
    internal readonly X509Certificate2[] TrustAnchors;
    private readonly X509RevocationMode _revocationMode;
    private readonly double _expiresAtTimestamp;
    /// <summary>Gets the original unencrypted resolver IP, which is the certificate identity and DoH origin.</summary>
    public IPAddress OriginalResolverAddress => CopyAddress(_originalAddress);
    /// <summary>Gets the original resolver's port.</summary>
    public int OriginalResolverPort { get; }
    /// <summary>Gets the selected ServiceMode binding.</summary>
    public DnsServiceBinding Binding { get; }
    /// <summary>Gets the connection address without changing the original authentication identity.</summary>
    public IPAddress ConnectionAddress => CopyAddress(_connectionAddress);
    /// <summary>Gets when the shortest designation, alias, or address lifetime expires.</summary>
    public DateTimeOffset ExpiresAt { get; }
    /// <summary>Gets the explicit verification outcome.</summary>
    public DnsDesignatedResolverVerification Verification { get; internal set; }
    /// <summary>Gets the failure or unsupported-transport explanation, when present.</summary>
    public string? VerificationError { get; internal set; }

    internal DnsDesignatedResolver(IPAddress original, int originalPort, DnsServiceBinding binding, IPAddress connection, DateTimeOffset expiresAt, double expiresAtTimestamp, X509Certificate2[] trustAnchors, X509RevocationMode revocationMode) {
        _originalAddress = CopyAddress(original);
        _connectionAddress = CopyAddress(connection);
        OriginalResolverPort = originalPort;
        Binding = binding;
        ExpiresAt = expiresAt;
        TrustAnchors = trustAnchors;
        _revocationMode = revocationMode;
        _expiresAtTimestamp = expiresAtTimestamp;
    }

    /// <summary>Creates a configuration for a still-valid verified designation, retaining the original IP identity.</summary>
    /// <param name = "usePost">Whether an HTTP binding should send DNS messages in a POST body.</param>
    /// <returns>An explicit encrypted configuration; failed or unverified discovery never falls back to cleartext.</returns>
    /// <exception cref="NotSupportedException">The transport cannot use its designated address while retaining the original identity.</exception>
    public Configuration CreateConfiguration(bool usePost = false) {
        if (Verification != DnsDesignatedResolverVerification.Verified) {
            throw new InvalidOperationException("Only a verified DDR candidate can create an encrypted resolver configuration.");
        }

        if (ExpiresAt <= DateTimeOffset.UtcNow || Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency >= _expiresAtTimestamp) {
            throw new InvalidOperationException("The DDR designation has expired. Repeat discovery for the original resolver on the current network.");
        }

        string? unsupportedReason = GetConfigurationUnsupportedReason();
        if (unsupportedReason != null) {
            throw new NotSupportedException(unsupportedReason);
        }

        DnsRequestFormat format = Binding.RequestFormat;
        Configuration configuration;
        if (Binding.DohPath != null) {
            if (usePost) {
                format = DnsRequestFormat.DnsOverHttpsWirePost;
            }

            string host = _originalAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? "[" + _originalAddress + "]" : _originalAddress.ToString();
            string template = "https://" + host + ":" + Binding.Port + Binding.DohPath.Template;
            configuration = new Configuration(new Uri(DnsDohUriTemplate.Parse(template).ExpandPost()), format) {
                DohUriTemplate = template,
                HttpVersion = new Version(Binding.Alpn == "h3" ? 3 : Binding.Alpn == "h2" ? 2 : 1, Binding.Alpn == "http/1.1" ? 1 : 0)
            };
        }
        else {
            configuration = new Configuration(_originalAddress.ToString(), format);
        }

        configuration.Port = Binding.Port;
        if (!_connectionAddress.Equals(_originalAddress)) {
            configuration.ConnectionAddress = CopyAddress(_connectionAddress);
        }

        configuration.TlsTrustAnchors = Array.AsReadOnly(TrustAnchors);
        configuration.TlsRevocationMode = _revocationMode;
        configuration.TlsServerName = DnsTlsCertificateTrust.NormalizeAuthenticationName(_originalAddress.ToString());
        configuration.UseTcpFallback = false;
        configuration.SelectHostNameStrategy();
        return configuration;
    }

    // Capability is address-dependent, so SVCB compatibility selection alone cannot establish it.
    internal string? GetConfigurationUnsupportedReason() {
        if (Binding.DohPath == null || _connectionAddress.Equals(_originalAddress)) {
            return null;
        }

        if (Binding.Alpn == "h3") {
            return "HTTP/3 DDR cannot use a different connection address while retaining the original resolver's HTTPS authority.";
        }

#if !NET8_0_OR_GREATER
        return "HTTP DDR with a different connection address and the original resolver's HTTPS authority requires .NET 8 or later.";
#else
        return null;
#endif
    }

    private static IPAddress CopyAddress(IPAddress address) => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? new IPAddress(address.GetAddressBytes(), address.ScopeId) : new IPAddress(address.GetAddressBytes());
}
