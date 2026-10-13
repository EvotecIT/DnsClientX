using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;
/// <summary>Protects resolver-bound DDR queries, explicit verification, and real network lifetime behavior.</summary>
public class DnsDesignatedResolverTests {
    /// <summary>Actual UDP alias queries stay with their original resolver, and inspection cannot auto-configure a client.</summary>
    [Fact]
    public async Task UdpDiscoveryKeepsAliasesAndHintsBoundToOriginalResolver() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancel = stop.Token.Register(server.Dispose);
        var names = new List<string>();
        Task responder = Task.Run(async () => {
            for (int index = 0; index < 2; index++) {
                UdpReceiveResult query = await server.ReceiveAsync();
                Assert.True(DnsWireMessageParser.TryParseQuestion(query.Buffer, 0, out DnsWireQuestionInfo question));
                names.Add(question.Name.TrimEnd('.'));
                Assert.Equal((ushort)DnsRecordType.SVCB, question.Type);
                var answer = index == 0 ? DnsDiscoveryTestWire.Svcb("_dns.resolver.arpa", 0, "_dns.alias.example", ttl: 5) : DnsDiscoveryTestWire.Svcb("_dns.alias.example", 1, "resolver.example", "h2", 8443, "/q{?dns}", IPAddress.Parse("192.0.2.10"), ttl: 60);
                byte[] response = DnsDiscoveryTestWire.Response(query.Buffer, answer);
                await server.SendAsync(response, response.Length, query.RemoteEndPoint);
            }
        });
        using var client = new ClientX(new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = port, TimeOut = 3000, UseTcpFallback = false });
        DateTimeOffset before = DateTimeOffset.UtcNow;
        DnsDesignatedResolver result = Assert.Single(await client.DiscoverDesignatedResolversAsync(new DnsDesignatedResolverDiscoveryOptions { Verify = false }, stop.Token));
        await responder;
        Assert.Equal(new[] { "_dns.resolver.arpa", "_dns.alias.example" }, names);
        Assert.Equal(IPAddress.Loopback, result.OriginalResolverAddress);
        Assert.Equal(port, result.OriginalResolverPort);
        Assert.Equal("192.0.2.10", result.ConnectionAddress.ToString());
        Assert.Equal(8443, result.Binding.Port);
        Assert.Equal(DnsDesignatedResolverVerification.NotAttempted, result.Verification);
        Assert.InRange((result.ExpiresAt - before).TotalSeconds, 0, 6);
        Assert.Throws<InvalidOperationException>(() => result.CreateConfiguration());
        Assert.Equal(DnsRequestFormat.DnsOverUDP, client.EndpointConfiguration.RequestFormat);
    }

    /// <summary>A real alias cycle terminates explicitly rather than retrying indefinitely.</summary>
    [Fact]
    public async Task UdpDiscoveryRejectsAliasCycles() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var registration = stop.Token.Register(server.Dispose);
        Task reply = Task.Run(async () => {
            UdpReceiveResult query = await server.ReceiveAsync();
            byte[] response = DnsDiscoveryTestWire.Response(query.Buffer, DnsDiscoveryTestWire.Svcb("_dns.resolver.arpa", 0, "_dns.resolver.arpa"));
            await server.SendAsync(response, response.Length, query.RemoteEndPoint);
        });
        using var client = new ClientX(new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = ((IPEndPoint)server.Client.LocalEndPoint!).Port, UseTcpFallback = false });
        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() => client.DiscoverDesignatedResolversAsync(new DnsDesignatedResolverDiscoveryOptions { Verify = false }, stop.Token));
        Assert.Contains("cycle", exception.Message);
        await reply;
    }

    /// <summary>Cancellation of a real pending UDP DDR query drains its socket before returning.</summary>
    [Fact]
    public async Task CancellationDrainsPendingUdpDiscovery() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var registration = stop.Token.Register(server.Dispose);
        using var queryStop = new CancellationTokenSource();
        using var client = new ClientX(new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = ((IPEndPoint)server.Client.LocalEndPoint!).Port, TimeOut = 10000, UseTcpFallback = false });
        Task<DnsDesignatedResolver[]> discovery = client.DiscoverDesignatedResolversAsync(new DnsDesignatedResolverDiscoveryOptions { Verify = false }, queryStop.Token);
        UdpReceiveResult query = await server.ReceiveAsync();
        queryStop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await discovery);
        using var releasedPort = new UdpClient();
        releasedPort.Client.ExclusiveAddressUse = true;
        releasedPort.Client.Bind(new IPEndPoint(IPAddress.Loopback, query.RemoteEndPoint.Port));
    }

    /// <summary>An exact IP SAN counts; DNS names and common names cannot substitute for the designating IP.</summary>
    [Fact]
    public void OriginalResolverRequiresExactIpSan() {
        using var certificate = new DnsDiscoveryCertificateFixture(IPAddress.Loopback);
        Assert.True(DnsResolverCertificateVerifier.ContainsResolverAddress(certificate.Leaf, IPAddress.Loopback));
        Assert.False(DnsResolverCertificateVerifier.ContainsResolverAddress(certificate.Leaf, IPAddress.Parse("127.0.0.2")));
        using var noIp = new DnsDiscoveryCertificateFixture(null);
        Assert.False(DnsResolverCertificateVerifier.ContainsResolverAddress(noIp.Leaf, IPAddress.Loopback));
    }

    /// <summary>An IPv6 scope selects the local interface and cannot appear in the SAN's address octets.</summary>
    [Fact]
    public void ScopedIpv6IdentityMatchesCertificateAddressOctets() {
        using var certificate = new DnsDiscoveryCertificateFixture(IPAddress.Parse("fe80::1"));
        Assert.True(DnsResolverCertificateVerifier.ContainsResolverAddress(certificate.Leaf, IPAddress.Parse("fe80::1%17")));
        Assert.False(DnsResolverCertificateVerifier.ContainsResolverAddress(certificate.Leaf, IPAddress.Parse("fe80::2%17")));
#if NET8_0_OR_GREATER
        Assert.True(DnsTlsCertificateTrust.Validate(certificate.Leaf, null, SslPolicyErrors.RemoteCertificateChainErrors,
            "fe80::1%17", new[] { certificate.Root }, X509RevocationMode.NoCheck));
#endif
    }

    /// <summary>Malformed DER SAN lengths and IP lengths fail rather than yielding an apparent address match.</summary>
    [Theory]
    [InlineData("300687047F00000100")]
    [InlineData("30810687047F000001")]
    [InlineData("300587037F0000")]
    public void RejectsMalformedSan(string hex) {
        byte[] encoded = Enumerable.Range(0, hex.Length / 2).Select(index => Convert.ToByte(hex.Substring(index * 2, 2), 16)).ToArray();
        using var fixture = new DnsDiscoveryCertificateFixture(null, encoded);
        Assert.False(DnsResolverCertificateVerifier.ContainsResolverAddress(fixture.Leaf, IPAddress.Loopback));
    }

#if NET8_0_OR_GREATER
    /// <summary>An ordinary DoT client preserves its IPv6 dial endpoint while authenticating the scoped literal's exact SAN address.</summary>
    [Fact]
    public async Task ScopedIpv6DotClientAuthenticatesWithoutScopeInSan() {
        using var fixture = new DnsDiscoveryCertificateFixture(IPAddress.IPv6Loopback);
        var listener = new TcpListener(IPAddress.IPv6Loopback, 0);
        listener.Start();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task server = Task.Run(async () => {
            using TcpClient peer = await listener.AcceptTcpClientAsync(stop.Token);
            using var tls = new SslStream(peer.GetStream());
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                ServerCertificate = fixture.Leaf, EnabledSslProtocols = SslProtocols.Tls12
            }, stop.Token);
            var length = new byte[2];
            await tls.ReadExactlyAsync(length, stop.Token);
            var query = new byte[(length[0] << 8) | length[1]];
            await tls.ReadExactlyAsync(query, stop.Token);
            byte[] response = DnsDiscoveryTestWire.Response(query, DnsDiscoveryTestWire.Address("scope.example", "192.0.2.56"));
            await tls.WriteAsync(new byte[] { (byte)(response.Length >> 8), (byte)response.Length }, stop.Token);
            await tls.WriteAsync(response, stop.Token);
        });
        try {
            var configuration = new Configuration("::1%17", DnsRequestFormat.DnsOverTLS) {
                Port = ((IPEndPoint)listener.LocalEndpoint).Port,
                ConnectionAddress = IPAddress.IPv6Loopback,
                TlsTrustAnchors = new[] { fixture.Root },
                TlsRevocationMode = X509RevocationMode.NoCheck
            };
            using var client = new ClientX(configuration);
            DnsResponse response = await client.Resolve("scope.example", retryOnTransient: false, cancellationToken: stop.Token);
            Assert.False(client.IgnoreCertificateErrors);
            Assert.Equal("192.0.2.56", Assert.Single(response.Answers).Data);
            await server;
        } finally {
            stop.Cancel();
            listener.Stop();
            try { await server; } catch (OperationCanceledException) { }
            catch (SocketException) { }
        }
    }

    /// <summary>Ephemeral private CA verification does not accept the right address with an untrusted chain or the wrong original IP.</summary>
    [Fact]
    public void CertificateVerificationNeedsBothTrustAndOriginalIp() {
        using var fixture = new DnsDiscoveryCertificateFixture(IPAddress.Loopback);
        Assert.True(DnsResolverCertificateVerifier.Verify(fixture.Leaf, IPAddress.Loopback, new[] { fixture.Root }, revocationMode: X509RevocationMode.NoCheck));
        Assert.False(DnsResolverCertificateVerifier.Verify(fixture.Leaf, IPAddress.Loopback, revocationMode: X509RevocationMode.NoCheck));
        Assert.False(DnsResolverCertificateVerifier.Verify(fixture.Leaf, IPAddress.Parse("127.0.0.2"), new[] { fixture.Root }, revocationMode: X509RevocationMode.NoCheck));
    }

    /// <summary>Actual DDR TLS verification accepts a trusted original-IP certificate and its returned configuration performs a real encrypted query.</summary>
    [Fact]
    public async Task VerifiedDdrConfigurationPerformsAuthenticatedDotQuery() {
        await RunTlsDiscovery(true);
    }

    /// <summary>A server proven usable by an authenticated DoT control still fails DDR when its certificate names a different original IP.</summary>
    [Fact]
    public async Task ValidCertificateForDifferentIpCannotRedirectDdr() {
        await RunTlsDiscovery(false);
    }

    private static async Task RunTlsDiscovery(bool matchingIp) {
        using var fixture = new DnsDiscoveryCertificateFixture(matchingIp ? IPAddress.Loopback : IPAddress.Parse("127.0.0.2"));
        using var dns = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var tlsListener = new TcpListener(IPAddress.Loopback, 0);
        tlsListener.Start();
        int encryptedPort = ((IPEndPoint)tlsListener.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var cancelDns = stop.Token.Register(dns.Dispose);
        Task dnsReply = Task.Run(async () => {
            UdpReceiveResult query = await dns.ReceiveAsync();
            byte[] response = DnsDiscoveryTestWire.Response(query.Buffer, DnsDiscoveryTestWire.Svcb("_dns.resolver.arpa", 1, "resolver.example", "dot", encryptedPort, hint: IPAddress.Loopback));
            await dns.SendAsync(response, query.RemoteEndPoint, stop.Token);
        });
        Task tlsServer = Task.Run(async () => {
            for (int attempt = 0; attempt < 2; attempt++) {
                using TcpClient peer = await tlsListener.AcceptTcpClientAsync(stop.Token);
                using var tls = new SslStream(peer.GetStream());
                try {
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = fixture.Leaf, EnabledSslProtocols = SslProtocols.Tls12, ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("dot") } }, stop.Token);
                    if (attempt == (matchingIp ? 1 : 0)) {
                        var length = new byte[2];
                        await tls.ReadExactlyAsync(length, stop.Token);
                        var query = new byte[(length[0] << 8) | length[1]];
                        await tls.ReadExactlyAsync(query, stop.Token);
                        Assert.True(DnsWireMessageParser.TryParseQuestion(query, 0, out DnsWireQuestionInfo question));
                        Assert.Equal("payload.example", question.Name.TrimEnd('.'));
                        byte[] response = DnsDiscoveryTestWire.Response(query, DnsDiscoveryTestWire.Address("payload.example", "192.0.2.55"));
                        await tls.WriteAsync(new byte[] { (byte)(response.Length >> 8), (byte)response.Length }, stop.Token);
                        await tls.WriteAsync(response, stop.Token);
                    }
                }
                catch (AuthenticationException)when (!matchingIp && attempt == 1) {
                }
                catch (System.IO.IOException)when (!matchingIp && attempt == 1) {
                }
            }
        });
        try {
            if (!matchingIp) {
                // The same negative fixture must first serve a real query authenticated for its own IP SAN.
                // Server credential/setup failures cannot masquerade as an original-resolver identity rejection.
                using var control = new ClientX(new Configuration("127.0.0.2", DnsRequestFormat.DnsOverTLS) {
                    Port = encryptedPort,
                    ConnectionAddress = IPAddress.Loopback,
                    TlsTrustAnchors = new[] { fixture.Root },
                    TlsRevocationMode = X509RevocationMode.NoCheck,
                    UseTcpFallback = false
                });
                Assert.False(control.IgnoreCertificateErrors);
                DnsResponse response = await control.Resolve("payload.example", retryOnTransient: false, cancellationToken: stop.Token);
                Assert.Equal(DnsResponseCode.NoError, response.Status);
                Assert.Equal("192.0.2.55", Assert.Single(response.Answers).Data);
            }

            using var original = new ClientX(new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = ((IPEndPoint)dns.Client.LocalEndPoint!).Port, UseTcpFallback = false }, ignoreCertificateErrors: !matchingIp);
            DnsDesignatedResolver designated = Assert.Single(await original.DiscoverDesignatedResolversAsync(new DnsDesignatedResolverDiscoveryOptions { TrustAnchors = new[] { fixture.Root }, RevocationMode = X509RevocationMode.NoCheck, VerificationTimeout = TimeSpan.FromSeconds(5) }, stop.Token));
            if (matchingIp) {
                Assert.Equal(DnsDesignatedResolverVerification.Verified, designated.Verification);
                Configuration configuration = designated.CreateConfiguration();
                Assert.Equal("127.0.0.1", configuration.Hostname);
                Assert.Null(configuration.ConnectionAddress);
                Assert.Single(configuration.TlsTrustAnchors!);
                using var encrypted = new ClientX(configuration);
                Assert.False(encrypted.IgnoreCertificateErrors);
                DnsResponse answer = await encrypted.Resolve("payload.example", retryOnTransient: false, cancellationToken: stop.Token);
                Assert.Equal("192.0.2.55", Assert.Single(answer.Answers).Data);
            }
            else {
                Assert.Equal(DnsDesignatedResolverVerification.Failed, designated.Verification);
                Assert.Throws<InvalidOperationException>(() => designated.CreateConfiguration());
            }

            await dnsReply;
            await tlsServer;
        }
        finally {
            stop.Cancel();
            tlsListener.Stop();
            try {
                await tlsServer;
            }
            catch (OperationCanceledException) {
            }
            catch (SocketException) {
            }

            try {
                await dnsReply;
            }
            catch (SocketException) {
            }
            catch (ObjectDisposedException) {
            }
        }
    }
#endif
}
