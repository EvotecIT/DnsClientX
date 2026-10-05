#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DnsClientX.Tests;

/// <summary>Observes the endpoint's real TLS identity without weakening certificate validation.</summary>
public class ResolverEndpointTlsIdentityTests {
    /// <summary>An IP-literal DoT target sends its configured DNS SNI and rejects an untrusted certificate.</summary>
    [Fact]
    public async Task ExplicitTlsNameReachesClientHelloWithCertificateValidationEnabled() {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=resolver.example", key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = TestUtilities.CreateTlsCertificate(request);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var observed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () => {
            using var peer = await listener.AcceptTcpClientAsync(deadline.Token);
            using var tls = new SslStream(peer.GetStream());
            try {
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                    ServerCertificateSelectionCallback = (_, name) => {
                        observed.TrySetResult(name ?? string.Empty);
                        return certificate;
                    },
                    EnabledSslProtocols = SslProtocols.Tls12
                }, deadline.Token);
            } catch (AuthenticationException) { }
            catch (System.IO.IOException) { }
        }, deadline.Token);
        try {
            using var client = ResolverEndpointClientFactory.CreateClient(new DnsResolverEndpoint {
                Transport = Transport.Dot, Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port,
                TlsServerName = "resolver.example", Timeout = TimeSpan.FromSeconds(3)
            });
            Assert.False(client.IgnoreCertificateErrors);
            var exception = await Assert.ThrowsAsync<DnsClientException>(() =>
                client.Resolve("payload.example", retryOnTransient: false, cancellationToken: deadline.Token));
            Assert.Equal("resolver.example", await observed.Task.WaitAsync(deadline.Token));
            Assert.Contains("AuthenticationException", exception.ToString());
            await server;
        } finally {
            deadline.Cancel();
            listener.Stop();
            try { await server; } catch (OperationCanceledException) { }
        }
    }
}
#endif
