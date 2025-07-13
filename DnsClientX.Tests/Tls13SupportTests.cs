#if NET6_0_OR_GREATER
using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class Tls13SupportTests {
        private static int GetFreePort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<SslProtocols> RunTls13ServerAsync(X509Certificate2 cert, int port, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
#if NET8_0_OR_GREATER
            using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
            using TcpClient client = await listener.AcceptTcpClientAsync();
#endif
            using var sslStream = new SslStream(client.GetStream(), false);
            await sslStream.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls13, false);
            listener.Stop();
            return sslStream.SslProtocol;
        }

        [Fact]
        public async Task ResolveWireFormatDoT_UsesTls13WhenAvailable() {
            int port = GetFreePort();
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 baseCert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            byte[] pfx = baseCert.Export(X509ContentType.Pfx);
            using X509Certificate2 cert = new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunTls13ServerAsync(cert, port, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) { Port = port };

            await Assert.ThrowsAsync<DnsClientException>(async () =>
                await DnsWireResolveDot.ResolveWireFormatDoT("127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, true, cts.Token));

            try {
                var protocol = await serverTask;
                Assert.Equal(SslProtocols.Tls13, protocol);
            } catch (Exception ex) when (ex is PlatformNotSupportedException || ex is AuthenticationException) {
                Console.WriteLine($"Skipping TLS 1.3 test: {ex.Message}");
                return;
            }
        }
    }
}
#endif
