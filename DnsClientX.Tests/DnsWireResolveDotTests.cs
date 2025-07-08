using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    [Collection("NoParallel")]
    public class DnsWireResolveDotTests {
        private static int GetFreePort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task RunInvalidTlsServerAsync(int port, X509Certificate2 cert, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
#if NET8_0_OR_GREATER
            using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
            using TcpClient client = await listener.AcceptTcpClientAsync();
#endif
            using var sslStream = new SslStream(client.GetStream(), false);
            try {
                await sslStream.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12, false).ConfigureAwait(false);
            } catch (AuthenticationException) {
                // ignore server-side auth failures
            }
            listener.Stop();
        }

        private static X509Certificate2 CreateInvalidCertificate() {
            using var ecdsa = ECDsa.Create();
            var req = new CertificateRequest("CN=invalid", ecdsa, HashAlgorithmName.SHA256);
            using X509Certificate2 cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
            byte[] pfx = cert.Export(X509ContentType.Pfx);
            return new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }

        [Fact]
        public async Task ResolveWireFormatDoT_ReturnsErrorWithCertificateDetails() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var cert = CreateInvalidCertificate();
            var serverTask = RunInvalidTlsServerAsync(port, cert, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) { Port = port };

            var response = await DnsWireResolveDot.ResolveWireFormatDoT("127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, false, cts.Token);

            Assert.Equal(DnsResponseCode.Refused, response.Status);
            Assert.Contains("certificate", response.Error, StringComparison.OrdinalIgnoreCase);

            await serverTask;
        }
    }
}
