using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests DNS over TLS resolution logic.
    /// </summary>
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
            try {
#if NET8_0_OR_GREATER
                using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
                using TcpClient client = await listener.AcceptTcpClientAsync();
#endif
                NetworkStream stream = client.GetStream();
                byte[] data = Encoding.ASCII.GetBytes("plain text");
                await stream.WriteAsync(data, 0, data.Length, token);
                await stream.FlushAsync(token);
            } finally {
                listener.Stop();
            }
        }

#if NET6_0_OR_GREATER
        private static async Task RunStallingTlsServerAsync(X509Certificate2 cert, int port, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            try {
#if NET8_0_OR_GREATER
                using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
                using TcpClient client = await listener.AcceptTcpClientAsync();
#endif
                using var sslStream = new SslStream(client.GetStream(), false);
                await sslStream.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);
                await Task.Delay(Timeout.Infinite, token);
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                // Expected during test shutdown.
            } finally {
                listener.Stop();
            }
        }
#endif

        /// <summary>
        /// Ensures authentication failures are converted to <see cref="DnsClientException"/>.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatDoT_ShouldWrapAuthenticationException() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunInvalidTlsServerAsync(port, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) { Port = port };

            var ex = await Assert.ThrowsAsync<DnsClientException>(async () =>
                await DnsWireResolveDot.ResolveWireFormatDoT("127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, true, cts.Token));

            Assert.NotNull(ex.Response);
            Assert.Equal(config.Hostname, ex.Response!.Questions[0].HostName);
            Assert.Equal(config.Port, ex.Response.Questions[0].Port);
            Assert.Equal(DnsResponseCode.ServerFailure, ex.Response.Status);
            Assert.NotEqual(DnsQueryErrorCode.None, ex.Response.ErrorCode);

            await serverTask;
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Ensures DoT response reads honor the configured endpoint timeout after TLS authentication succeeds.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatDoT_ShouldHonorConfiguredReadTimeout() {
            int port = GetFreePort();
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 baseCert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            byte[] pfx = baseCert.Export(X509ContentType.Pfx);
#if NET9_0_OR_GREATER
            using X509Certificate2 cert = X509CertificateLoader.LoadPkcs12(
                pfx,
                password: null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet,
                Pkcs12LoaderLimits.Defaults);
#else
            using X509Certificate2 cert = new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
#endif

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunStallingTlsServerAsync(cert, port, cts.Token);
            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) {
                Port = port,
                TimeOut = 150
            };

            var sw = Stopwatch.StartNew();
            var ex = await Assert.ThrowsAsync<DnsClientException>(async () =>
                await DnsWireResolveDot.ResolveWireFormatDoT("127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, true, cts.Token));
            sw.Stop();

            Assert.NotNull(ex.Response);
            Assert.Equal(DnsResponseCode.ServerFailure, ex.Response!.Status);
            Assert.Equal(DnsQueryErrorCode.Timeout, ex.Response.ErrorCode);
            Assert.IsType<TimeoutException>(ex.InnerException);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Expected configured timeout to fire quickly, but took {sw.Elapsed}.");

            cts.Cancel();
            await serverTask;
        }
#endif
    }
}
