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
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;

                var serverTask = Task.Run(async () => {
                    using TcpClient serverClient = await listener.AcceptTcpClientAsync();
                    using NetworkStream stream = serverClient.GetStream();
                    var buffer = new byte[1];
                    try {
                        while (await stream.ReadAsync(buffer, 0, 1) != 0) {
                        }
                        return true;
                    } catch {
                        // any exception indicates the client closed or aborted
                        return true;
                    }
                });

                var config = new Configuration("localhost", DnsRequestFormat.DnsOverTLS) { Port = port };

                await Assert.ThrowsAsync<IOException>(async () =>
                    await DnsWireResolveDot.ResolveWireFormatDoT("localhost", port, "example.com", DnsRecordType.A,
                        false, false, false, config, CancellationToken.None));

                Assert.True(await serverTask); // server observed client disconnect
            } finally {
                listener.Stop();
            }
        }
    }
}
