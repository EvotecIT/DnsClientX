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
    public class DnsWireResolveDotDisposeTests {
        private static X509Certificate2 CreateCertificate() {
            using var ecdsa = ECDsa.Create();
            var req = new CertificateRequest("CN=localhost", ecdsa, HashAlgorithmName.SHA256);
            return req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
        }

        [Fact]
        public async Task ResolveWireFormatDoT_DisposesTcpClientAndSslStream() {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var serverTask = Task.Run(async () => {
                using TcpClient serverClient = await listener.AcceptTcpClientAsync();
                using var sslServer = new SslStream(serverClient.GetStream(), false);
                using X509Certificate2 cert = CreateCertificate();
                await sslServer.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12, false);
                var buffer = new byte[2];
                int read = await sslServer.ReadAsync(buffer, 0, 2);
                return read == 0; // true if client closed connection
            });

            var config = new Configuration("localhost", DnsRequestFormat.DnsOverTLS) { Port = port };

            await Assert.ThrowsAsync<Exception>(async () =>
                await DnsWireResolveDot.ResolveWireFormatDoT("localhost", port, "example.com", DnsRecordType.A,
                    false, false, false, config, CancellationToken.None));

            Assert.True(await serverTask); // server observed client disconnect
        }
    }
}
