using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests DNS over TLS resolution logic.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsWireResolveDotTests {
        private static async Task RunRejectingTlsServerAsync(TcpListener listener, TaskCompletionSource<bool> clientHelloReceived, CancellationToken token) {
            try {
#if NET8_0_OR_GREATER
                using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
                using TcpClient client = await listener.AcceptTcpClientAsync();
#endif
                NetworkStream stream = client.GetStream();
                // Consume ClientHello before replying and keep the peer alive until the assertion.
                // Closing with unread input can reset the socket instead of failing TLS authentication.
                var header = new byte[5];
                await TestUtilities.ReadExactlyAsync(stream, header, header.Length, token);
                Assert.Equal(22, header[0]);
                int recordLength = (header[3] << 8) | header[4];
                Assert.InRange(recordLength, 1, 16384);
                var hello = new byte[recordLength];
                await TestUtilities.ReadExactlyAsync(stream, hello, hello.Length, token);
                clientHelloReceived.TrySetResult(true);
                // A fatal handshake_failure alert is complete TLS framing on both SChannel and OpenSSL.
                // Arbitrary plaintext can leave the .NET Framework TLS reader waiting for more bytes.
                byte[] data = { 21, 3, 3, 0, 2, 2, 40 };
                await stream.WriteAsync(data, 0, data.Length, token);
                await stream.FlushAsync(token);
                await Task.Delay(Timeout.Infinite, token);
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                // Expected after the client assertion completes.
            } catch (SocketException) when (token.IsCancellationRequested) {
                // Stopping the listener also releases the non-cancelable .NET Framework accept.
            } catch (System.IO.IOException) when (token.IsCancellationRequested) {
                // Test shutdown can interrupt a pending read or write.
            } finally {
                listener.Stop();
            }
        }

#if NET6_0_OR_GREATER
        private static async Task RunStallingTlsServerAsync(X509Certificate2 cert, TcpListener listener,
            TaskCompletionSource<bool> queryReceived, CancellationToken token) {
            try {
#if NET8_0_OR_GREATER
                using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
                using TcpClient client = await listener.AcceptTcpClientAsync();
#endif
                using var sslStream = new SslStream(client.GetStream(), false);
                await sslStream.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);
                var queryPrefix = new byte[2];
                int bytesRead = await sslStream.ReadAsync(queryPrefix, 0, queryPrefix.Length, token);
                Assert.True(bytesRead > 0, "The client closed the TLS connection before sending a DNS query.");
                queryReceived.TrySetResult(true);
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
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var clientHelloReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var serverTask = RunRejectingTlsServerAsync(listener, clientHelloReceived, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) { Port = port };

            try {
                var ex = await Assert.ThrowsAsync<DnsClientException>(async () =>
                    await DnsWireResolveDot.ResolveWireFormatDoT("127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, true, cts.Token));

                Assert.True(clientHelloReceived.Task.IsCompleted, "The server must consume ClientHello before the authentication failure is asserted.");
                Exception? authenticationFailure = ex;
                while (authenticationFailure != null && authenticationFailure is not AuthenticationException) {
                    authenticationFailure = authenticationFailure.InnerException;
                }
                Assert.True(authenticationFailure is AuthenticationException, $"Expected a TLS authentication failure, received: {ex}");
                Assert.NotNull(ex.Response);
                Assert.Equal(config.Hostname, ex.Response!.Questions[0].HostName);
                Assert.Equal(config.Port, ex.Response.Questions[0].Port);
                Assert.Equal(DnsResponseCode.ServerFailure, ex.Response.Status);
                Assert.Equal(DnsQueryErrorCode.ServFail, ex.Response.ErrorCode);
            } finally {
                cts.Cancel();
                listener.Stop();
                await serverTask;
            }
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Ensures DoT response reads honor the configured endpoint timeout after TLS authentication succeeds.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatDoT_ShouldHonorConfiguredReadTimeout() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 cert = TestUtilities.CreateTlsCertificate(request);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var queryReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var serverTask = RunStallingTlsServerAsync(cert, listener, queryReceived, cts.Token);
            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) {
                Port = port,
                TimeOut = 3500
            };

            var sw = Stopwatch.StartNew();
            try {
                var ex = await Assert.ThrowsAsync<DnsClientException>(async () =>
                    await DnsWireResolveDot.ResolveWireFormatDoT("127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, true, cts.Token));
                sw.Stop();

                Assert.True(queryReceived.Task.IsCompletedSuccessfully,
                    "The server must receive a DNS query before the response-read timeout is asserted.");
                Assert.NotNull(ex.Response);
                Assert.Equal(DnsResponseCode.ServerFailure, ex.Response!.Status);
                Assert.Equal(DnsQueryErrorCode.Timeout, ex.Response.ErrorCode);
                Assert.IsType<TimeoutException>(ex.InnerException);
                Assert.InRange(sw.ElapsedMilliseconds, 3000, 7000);
            } finally {
                cts.Cancel();
                await serverTask;
            }
        }
#endif
    }
}
