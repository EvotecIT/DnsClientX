using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Shows how <see cref="ClientX"/> exposes TLS certificate failures when using DoT.
    /// </summary>
    internal class DemoCertificateError {
        public static async Task Example() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunInvalidCertificateServerAsync(port, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTLS);
            client.EndpointConfiguration.Port = port;
            try {
                await client.Resolve("example.com", DnsRecordType.A, cancellationToken: cts.Token);
            } catch (DnsClientException ex) {
                Console.WriteLine(ex.Response.Error);
            }

            // Output includes the certificate validation failure

            await serverTask;
        }

        private static int GetFreePort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static X509Certificate2 CreateSelfSignedCertificate(string subject) {
            using var ecdsa = ECDsa.Create();
            var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
            return request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
        }

        private static async Task RunInvalidCertificateServerAsync(int port, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
#if NET8_0_OR_GREATER
            using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
            using TcpClient client = await listener.AcceptTcpClientAsync();
#endif
            using var ssl = new SslStream(client.GetStream(), false);
            using var cert = CreateSelfSignedCertificate("CN=localhost");
            await ssl.AuthenticateAsServerAsync(cert, false, false);
#if NET8_0_OR_GREATER
            await ssl.WriteAsync(new byte[] { 0 }, 0, 1, token);
            await ssl.FlushAsync(token);
#else
            await ssl.WriteAsync(new byte[] { 0 }, 0, 1);
            await ssl.FlushAsync();
#endif
            listener.Stop();
        }
    }}
