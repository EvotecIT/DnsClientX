using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.Security;
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

        private static async Task RunInvalidTlsServerAsync(int port, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
#if NET8_0_OR_GREATER
            using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
            using TcpClient client = await listener.AcceptTcpClientAsync();
#endif
            NetworkStream stream = client.GetStream();
            byte[] data = Encoding.ASCII.GetBytes("plain text");
#if NET8_0_OR_GREATER
            await stream.WriteAsync(data, 0, data.Length, token);
            await stream.FlushAsync(token);
#else
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
#endif
            listener.Stop();
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

        [Fact]
        public async Task ResolveWireFormatDoT_ShouldWrapAuthenticationException() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunInvalidTlsServerAsync(port, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) { Port = port };

            var ex = await Assert.ThrowsAsync<DnsClientException>(async () =>
                await DnsWireResolveDot.ResolveWireFormatDoT("127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, true, cts.Token));

            Assert.Equal(config.Hostname, ex.Response.Questions[0].HostName);
            Assert.Equal(config.Port, ex.Response.Questions[0].Port);

            await serverTask;
        }

        [Fact]
        public async Task ResolveWireFormatDoT_ShouldReturnDetailedCertificateError() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunInvalidCertificateServerAsync(port, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) { Port = port };

            var ex = await Assert.ThrowsAsync<DnsClientException>(async () =>
                await DnsWireResolveDot.ResolveWireFormatDoT("127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, false, cts.Token));

            Assert.True(
                ex.Response.Error.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
                ex.Response.Error.Contains("handshake", StringComparison.OrdinalIgnoreCase),
                $"Unexpected error: {ex.Response.Error}");
            await serverTask;
        }
    }
}
