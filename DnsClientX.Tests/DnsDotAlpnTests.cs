#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace DnsClientX.Tests;

/// <summary>Protects ordinary authenticated DoT against peers that require the advertised dot protocol.</summary>
public sealed class DnsDotAlpnTests {
    /// <summary>A strict peer receives dot ALPN and the configured SNI for two queries over the same authenticated stream.</summary>
    [Fact]
    public async Task OrdinaryDotNegotiatesDotAndRetainsTlsIdentityAcrossPooledQueries() {
        using var fixture = new DnsDiscoveryCertificateFixture(IPAddress.Loopback);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task server = Task.Run(async () => {
            using TcpClient peer = await listener.AcceptTcpClientAsync(deadline.Token);
            using var tls = new SslStream(peer.GetStream());
            string? serverName = null;
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                ServerCertificateSelectionCallback = (_, name) => { serverName = name; return fixture.Leaf; },
                EnabledSslProtocols = SslProtocols.Tls12,
                ApplicationProtocols = new List<SslApplicationProtocol> { new("dot") }
            }, deadline.Token);
            Assert.Equal("resolver.example", serverName);
            Assert.Equal(new SslApplicationProtocol("dot"), tls.NegotiatedApplicationProtocol);
            for (int index = 0; index < 2; index++) {
                var length = new byte[2];
                await tls.ReadExactlyAsync(length, deadline.Token);
                var query = new byte[(length[0] << 8) | length[1]];
                await tls.ReadExactlyAsync(query, deadline.Token);
                byte[] response = DnsDiscoveryTestWire.Response(query,
                    DnsDiscoveryTestWire.Address("dot-alpn.example", "192.0.2.14"));
                await tls.WriteAsync(new byte[] { (byte)(response.Length >> 8), (byte)response.Length }, deadline.Token);
                await tls.WriteAsync(response, deadline.Token);
            }
        });
        try {
            using var client = new ClientX(new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) {
                Port = ((IPEndPoint)listener.LocalEndpoint).Port,
                TlsServerName = "resolver.example",
                TlsTrustAnchors = new[] { fixture.Root },
                TlsRevocationMode = X509RevocationMode.NoCheck,
                UseTcpFallback = false
            });
            Assert.False(client.IgnoreCertificateErrors);
            for (int index = 0; index < 2; index++) {
                DnsResponse response = await client.Resolve("dot-alpn.example", retryOnTransient: false,
                    cancellationToken: deadline.Token);
                Assert.Equal(DnsResponseCode.NoError, response.Status);
                Assert.Equal("192.0.2.14", Assert.Single(response.Answers).Data);
            }
            await server;
        } finally {
            deadline.Cancel();
            listener.Stop();
            try { await server; }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
            catch (SocketException) when (deadline.IsCancellationRequested) { }
        }
    }
}
#endif
