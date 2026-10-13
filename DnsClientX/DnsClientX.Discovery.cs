using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
#if NET8_0_OR_GREATER
using System.Net.Quic;

// QuicConnection.IsSupported gates the optional runtime path, matching the core QUIC pool.
#pragma warning disable CA2252, CA1416
#endif
namespace DnsClientX;
public partial class ClientX {
    /// <summary>Queries the current unencrypted IP resolver for RFC 9462 designated encrypted resolvers.</summary>
    /// <param name = "options">Bounded discovery and explicit certificate trust policy.</param>
    /// <param name = "cancellationToken">Cancellation for DNS queries and all verification connections.</param>
    /// <returns>Per-resolver candidates with explicit verification outcomes, ordered by SVCB priority and ALPN preference.</returns>
    /// <remarks>The query to <c>_dns.resolver.arpa</c> and all alias/address queries stay pinned to the original resolver.
    /// Discovery does not mutate the client, automatically switch protocols, or disable certificate checks.</remarks>
    public async Task<DnsDesignatedResolver[]> DiscoverDesignatedResolversAsync(DnsDesignatedResolverDiscoveryOptions? options = null, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        options ??= new DnsDesignatedResolverDiscoveryOptions();
        ValidateDiscoveryOptions(options);
        bool verify = options.Verify;
        int maximumHops = options.MaximumAliasHops;
        int maximumCandidates = options.MaximumCandidates;
        TimeSpan verificationTimeout = options.VerificationTimeout;
        X509RevocationMode revocation = options.RevocationMode;
        string[]? protocols = options.SupportedAlpn?.ToArray();
        X509Certificate2[] roots = (options.TrustAnchors ?? Array.Empty<X509Certificate2>()).ToArray();
        Configuration original = EndpointConfiguration.CreateQuerySnapshot();
        if (original.RequestFormat is not (DnsRequestFormat.DnsOverUDP or DnsRequestFormat.DnsOverTCP) || !IPAddress.TryParse(original.Hostname, out IPAddress? originalAddress)) {
            throw new InvalidOperationException("IP-based DDR requires an explicit UDP or TCP resolver IP address.");
        }

        originalAddress = original.ConnectionAddress ?? originalAddress;
        var pinnedConfiguration = new Configuration(originalAddress.ToString(), original.RequestFormat) {
            Port = original.Port,
            TimeOut = original.TimeOut,
            UseTcpFallback = original.UseTcpFallback,
            LocalEndPoint = original.LocalEndPoint,
            WaitForCanceledStreamQueryDrain = true
        };
        using var resolver = new ClientX(pinnedConfiguration, enableCache: false);
        string owner = "_dns.resolver.arpa";
        int aliasTtl = int.MaxValue;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<DnsDesignatedResolver>();
        DateTimeOffset receipt = DateTimeOffset.UtcNow;
        double receiptTimestamp = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        for (int hop = 0; hop <= maximumHops; hop++) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(DnsWireNameCodec.Canonical(owner))) {
                throw new DnsClientException("DDR SVCB aliases contain a cycle.");
            }

            DnsResponse response = await resolver.QueryDesignationRecord(owner, DnsRecordType.SVCB, cancellationToken).ConfigureAwait(false);
            if (response.Status is DnsResponseCode.NXDomain or DnsResponseCode.NXRRSet) {
                return Array.Empty<DnsDesignatedResolver>();
            }

            DnsAnswer[] answers = (response.Answers ?? Array.Empty<DnsAnswer>()).Where(answer => answer.Type == DnsRecordType.SVCB && SameDnsOwner(answer.Name, owner)).ToArray();
            var bindings = answers.Select(answer => (Answer: answer, Record: answer.TypedRecord as SvcbRecord)).Where(item => item.Record != null).ToArray();
            var aliases = bindings.Where(item => item.Record!.IsAliasMode).ToArray();
            string? alias = aliases.Length > 0 ? aliases[0].Record!.Target : FindAliasTarget(response, owner);
            if (alias != null) {
                if (alias == ".") {
                    return Array.Empty<DnsDesignatedResolver>();
                }

                aliasTtl = Math.Min(aliasTtl, aliases.Length > 0 ? Math.Max(0, aliases[0].Answer.TTL) : Math.Max(0, (response.Answers ?? Array.Empty<DnsAnswer>()).Where(answer => answer.Type is DnsRecordType.CNAME or DnsRecordType.DNAME).Min(answer => answer.TTL)));
                if (hop == maximumHops) {
                    throw new DnsClientException("DDR SVCB alias depth exceeds the configured bound.");
                }

                owner = DnsWireNameCodec.Normalize(alias);
                continue;
            }

            DnsServiceBinding[] selected = DnsServiceBindingSelection.SelectDnsResolvers(owner, bindings.Select(item => item.Record!), protocols);
            foreach (DnsServiceBinding binding in selected) {
                int ttl = Math.Min(aliasTtl, Math.Max(0, bindings.First(item => ReferenceEquals(item.Record, binding.Record)).Answer.TTL));
                (IPAddress[] addresses, int addressTtl) = await resolver.ResolveDesignationAddresses(binding, response, maximumHops, cancellationToken).ConfigureAwait(false);
                ttl = Math.Min(ttl, addressTtl);
                foreach (IPAddress address in addresses) {
                    if (results.Count == maximumCandidates) {
                        throw new DnsClientException("DDR candidates exceed the configured bound.");
                    }

                    IPAddress connection = address;
                    if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv6LinkLocal
                        && address.ScopeId == 0 && originalAddress.AddressFamily == AddressFamily.InterNetworkV6 && originalAddress.ScopeId != 0) {
                        connection = new IPAddress(address.GetAddressBytes(), originalAddress.ScopeId);
                    }
                    var candidate = new DnsDesignatedResolver(originalAddress, original.Port, binding, connection,
                        receipt.AddSeconds(ttl), receiptTimestamp + ttl, roots, revocation);
                    if (verify) {
                        await VerifyDesignationAsync(candidate, verificationTimeout, roots, revocation, cancellationToken).ConfigureAwait(false);
                    }

                    results.Add(candidate);
                }
            }

            return results.ToArray();
        }

        throw new DnsClientException("DDR SVCB alias depth exceeds the configured bound.");
    }

    private async Task<DnsResponse> QueryDesignationRecord(string owner, DnsRecordType type, CancellationToken cancellationToken) {
        try {
            DnsResponse response = await Resolve(owner, type, returnAllTypes: true, retryOnTransient: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.Status is not (DnsResponseCode.NoError or DnsResponseCode.NXDomain or DnsResponseCode.NXRRSet)) {
                throw new DnsClientException(response.Error ?? "DDR query failed: " + response.Status, response);
            }

            return response;
        }
        catch (DnsClientException exception)when (exception.Response?.Status is DnsResponseCode.NXDomain or DnsResponseCode.NXRRSet) {
            return exception.Response;
        }
    }

    private async Task<(IPAddress[] Addresses, int Ttl)> ResolveDesignationAddresses(DnsServiceBinding binding, DnsResponse response, int maximumHops, CancellationToken cancellationToken) {
        DnsAnswer[] advertised = (response.Answers ?? Array.Empty<DnsAnswer>()).Concat(response.Additional ?? Array.Empty<DnsAnswer>()).Where(answer => SameDnsOwner(answer.Name, binding.Target) && answer.Type is DnsRecordType.A or DnsRecordType.AAAA).ToArray();
        var addresses = new List<IPAddress>();
        int ttl = int.MaxValue;
        foreach (DnsAnswer answer in advertised) {
            if (IPAddress.TryParse(answer.Data, out IPAddress? address)) {
                addresses.Add(address);
                ttl = Math.Min(ttl, Math.Max(0, answer.TTL));
            }
        }

        if (addresses.Count == 0 && binding.AddressHints.Count == 0) {
            foreach (DnsRecordType type in new[] { DnsRecordType.A, DnsRecordType.AAAA }) {
                DnsResponse addressResponse = await QueryDesignationRecord(binding.Target, type, cancellationToken).ConfigureAwait(false);
                string candidate = binding.Target;
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int hop = 0; hop <= maximumHops; hop++) {
                    if (!visited.Add(DnsWireNameCodec.Canonical(candidate))) {
                        throw new DnsClientException("DDR address aliases contain a cycle.");
                    }

                    foreach (DnsAnswer answer in (addressResponse.Answers ?? Array.Empty<DnsAnswer>()).Where(answer => SameDnsOwner(answer.Name, candidate) && answer.Type == type)) {
                        if (IPAddress.TryParse(answer.Data, out IPAddress? address)) {
                            addresses.Add(address);
                            ttl = Math.Min(ttl, Math.Max(0, answer.TTL));
                        }
                    }

                    string? alias = FindAliasTarget(addressResponse, candidate);
                    if (alias == null) {
                        break;
                    }

                    foreach (DnsAnswer dependency in (addressResponse.Answers ?? Array.Empty<DnsAnswer>()).Where(answer => answer.Type is DnsRecordType.CNAME or DnsRecordType.DNAME)) {
                        ttl = Math.Min(ttl, Math.Max(0, dependency.TTL));
                    }

                    if (hop == maximumHops) {
                        throw new DnsClientException("DDR address alias depth exceeds the configured bound.");
                    }

                    candidate = alias;
                    if (!(addressResponse.Answers ?? Array.Empty<DnsAnswer>()).Any(answer => SameDnsOwner(answer.Name, candidate))) {
                        addressResponse = await QueryDesignationRecord(candidate, type, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        if (addresses.Count == 0) {
            addresses.AddRange(binding.AddressHints);
        }

        return (addresses.Distinct().ToArray(), ttl);
    }

    private static async Task VerifyDesignationAsync(DnsDesignatedResolver candidate, TimeSpan timeout, X509Certificate2[] roots, X509RevocationMode revocation, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        string? unsupportedReason = candidate.GetConfigurationUnsupportedReason();
        if (unsupportedReason != null) {
            candidate.Verification = DnsDesignatedResolverVerification.Unsupported;
            candidate.VerificationError = unsupportedReason;
            return;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        bool identityPassed = false;
        string authenticationName = DnsTlsCertificateTrust.NormalizeAuthenticationName(candidate.OriginalResolverAddress.ToString());
        bool ValidateCertificate(object _, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors) {
            if (certificate == null || (errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0) {
                return false;
            }

            using var leaf = DnsTlsCertificateTrust.LoadCertificate(certificate.GetRawCertData());
            if (!DnsResolverCertificateVerifier.ContainsResolverAddress(leaf, candidate.OriginalResolverAddress)) {
                return false;
            }

            identityPassed = DnsTlsCertificateTrust.Validate(certificate, chain, errors, candidate.OriginalResolverAddress.ToString(), roots, revocation);
            return identityPassed;
        }

        try {
            if (candidate.Binding.Alpn is "h3" or "doq") {
#if NET8_0_OR_GREATER
                if (!QuicConnection.IsSupported) {
                    candidate.Verification = DnsDesignatedResolverVerification.Unsupported;
                    candidate.VerificationError = "The current host does not support QUIC TLS verification.";
                    return;
                }

                await using var connection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions {
                    RemoteEndPoint = new IPEndPoint(candidate.ConnectionAddress, candidate.Binding.Port),
                    DefaultCloseErrorCode = 0,
                    DefaultStreamErrorCode = 0,
                    ClientAuthenticationOptions = new SslClientAuthenticationOptions {
                        TargetHost = authenticationName,
                        ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol(candidate.Binding.Alpn) },
                        CertificateRevocationCheckMode = revocation,
                        RemoteCertificateValidationCallback = ValidateCertificate
                    }
                }, deadline.Token).ConfigureAwait(false);
#else
                candidate.Verification = DnsDesignatedResolverVerification.Unsupported;
                candidate.VerificationError = "QUIC designated resolver verification requires modern .NET and a supported host.";
                return;
#endif
            } else {
                using var tcp = new TcpClient(candidate.ConnectionAddress.AddressFamily);
                using CancellationTokenRegistration registration = deadline.Token.Register(tcp.Dispose);
#if NET8_0_OR_GREATER
                await tcp.ConnectAsync(candidate.ConnectionAddress, candidate.Binding.Port, deadline.Token).ConfigureAwait(false);
#else
                await tcp.ConnectAsync(candidate.ConnectionAddress, candidate.Binding.Port).ConfigureAwait(false);
#endif
                using var tls = new SslStream(tcp.GetStream(), false, ValidateCertificate);
#if NET8_0_OR_GREATER
                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
                    TargetHost = authenticationName,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol(candidate.Binding.Alpn) },
                    CertificateRevocationCheckMode = revocation
                }, deadline.Token).ConfigureAwait(false);
                if (candidate.Binding.Alpn == "h2" && tls.NegotiatedApplicationProtocol != SslApplicationProtocol.Http2) {
                    throw new AuthenticationException("The designated resolver did not negotiate its advertised HTTP/2 ALPN.");
                }
#else
                await tls.AuthenticateAsClientAsync(authenticationName, null, SslProtocols.Tls12, revocation != X509RevocationMode.NoCheck).ConfigureAwait(false);
                if (candidate.Binding.Alpn == "h2") {
                    candidate.Verification = DnsDesignatedResolverVerification.Unsupported;
                    candidate.VerificationError = "HTTP/2 ALPN verification requires modern .NET.";
                    return;
                }
#endif
            }

            deadline.Token.ThrowIfCancellationRequested();
            if (!identityPassed) {
                throw new AuthenticationException("The designated resolver did not prove the original resolver's IP identity.");
            }

            candidate.Verification = DnsDesignatedResolverVerification.Verified;
        } catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is SocketException or AuthenticationException or System.IO.IOException or OperationCanceledException or ObjectDisposedException
#if NET8_0_OR_GREATER
        or QuicException
#endif
        ) {
            candidate.Verification = DnsDesignatedResolverVerification.Failed;
            candidate.VerificationError = deadline.IsCancellationRequested ? "Designated resolver verification timed out." : exception.Message;
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateDiscoveryOptions(DnsDesignatedResolverDiscoveryOptions options) {
        if (options.MaximumAliasHops < 0 || options.MaximumAliasHops > 64) {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumAliasHops));
        }

        if (options.MaximumCandidates < 1 || options.MaximumCandidates > 1024) {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumCandidates));
        }

        if (options.VerificationTimeout <= TimeSpan.Zero || options.VerificationTimeout.TotalMilliseconds > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(options.VerificationTimeout));
        }

        if (!Enum.IsDefined(typeof(X509RevocationMode), options.RevocationMode)) {
            throw new ArgumentOutOfRangeException(nameof(options.RevocationMode));
        }
#if !NET8_0_OR_GREATER
        if (options.TrustAnchors?.Count > 0) {
            throw new PlatformNotSupportedException("Custom application TLS roots require modern .NET.");
        }
#endif
    }

    private static bool SameDnsOwner(string first, string second) => string.Equals(DnsWireNameCodec.Canonical(first), DnsWireNameCodec.Canonical(second), StringComparison.Ordinal);
}
#if NET8_0_OR_GREATER
#pragma warning restore CA2252, CA1416
#endif
