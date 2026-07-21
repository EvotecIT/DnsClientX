using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>Protects RFC 7766 connection reuse, pipelining, and response dispatch.</summary>
    [Collection("NoParallel")]
    public class DnsStreamConnectionPoolTests {
        /// <summary>Sequential TCP queries reuse one connection owned by the high-level client.</summary>
        [Fact]
        public async Task TcpQueriesReuseOneConnection() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            int accepted = 0;
            Task server = Task.Run(async () => {
                using TcpClient connection = await AcceptAsync(listener, timeout.Token);
                Interlocked.Increment(ref accepted);
                NetworkStream stream = connection.GetStream();
                for (int index = 0; index < 2; index++) {
                    byte[] query = await ReadFrameAsync(stream, timeout.Token);
                    await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(query), timeout.Token);
                }
            }, timeout.Token);

            try {
                var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                    Port = port,
                    TimeOut = 2000
                };
                using var client = new ClientX(configuration);

                DnsResponse first = await client.Resolve("first.example", retryOnTransient: false,
                    cancellationToken: timeout.Token);
                DnsResponse second = await client.Resolve("second.example", retryOnTransient: false,
                    cancellationToken: timeout.Token);
                await server;

                Assert.Equal(DnsResponseCode.NoError, first.Status);
                Assert.Equal(DnsResponseCode.NoError, second.Status);
                Assert.Equal(1, Volatile.Read(ref accepted));
            } finally {
                listener.Stop();
            }
        }

        /// <summary>Concurrent TCP queries are sent before either response and dispatch correctly out of order.</summary>
        [Fact]
        public async Task TcpPipeliningDispatchesOutOfOrderResponses() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Task server = RunOutOfOrderServerAsync(listener, useTls: false, certificate: null, timeout.Token);

            try {
                var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                    Port = port,
                    TimeOut = 2000
                };
                using var client = new ClientX(configuration);
                Task<DnsResponse> firstTask = client.Resolve("first.example", retryOnTransient: false,
                    cancellationToken: timeout.Token);
                Task<DnsResponse> secondTask = client.Resolve("second.example", retryOnTransient: false,
                    cancellationToken: timeout.Token);

                DnsResponse[] responses = await Task.WhenAll(firstTask, secondTask);
                await server;

                Assert.Equal("first.example", Assert.Single(responses[0].Questions).Name);
                Assert.Equal("second.example", Assert.Single(responses[1].Questions).Name);
            } finally {
                listener.Stop();
            }
        }

        /// <summary>A canceled transaction and its late response do not poison other work on the connection.</summary>
        [Fact]
        public async Task CancelledQueryDoesNotPoisonSharedConnection() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Task server = Task.Run(async () => {
                using TcpClient connection = await AcceptAsync(listener, timeout.Token);
                NetworkStream stream = connection.GetStream();
                byte[] first = await ReadFrameAsync(stream, timeout.Token);
                byte[] second = await ReadFrameAsync(stream, timeout.Token);
                string firstName = await GetQuestionNameAsync(first);
                byte[] slow = string.Equals(firstName, "slow.example", StringComparison.Ordinal) ? first : second;
                byte[] fast = ReferenceEquals(slow, first) ? second : first;

                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(fast), timeout.Token);
                await Task.Delay(250, timeout.Token);
                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(slow), timeout.Token);
                byte[] third = await ReadFrameAsync(stream, timeout.Token);
                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(third), timeout.Token);
            }, timeout.Token);

            try {
                var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                    Port = port,
                    TimeOut = 2000
                };
                using var client = new ClientX(configuration);
                using var slowCancellation = new CancellationTokenSource(100);
                Task<DnsResponse> slowTask = client.Resolve("slow.example", retryOnTransient: false,
                    cancellationToken: slowCancellation.Token);
                Task<DnsResponse> fastTask = client.Resolve("fast.example", retryOnTransient: false,
                    cancellationToken: timeout.Token);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slowTask);
                DnsResponse fast = await fastTask;
                DnsResponse third = await client.Resolve("third.example", retryOnTransient: false,
                    cancellationToken: timeout.Token);
                await server;

                Assert.Equal("fast.example", Assert.Single(fast.Questions).Name);
                Assert.Equal("third.example", Assert.Single(third.Questions).Name);
            } finally {
                listener.Stop();
            }
        }

        /// <summary>A connection closed by the server is replaced before the next standard query.</summary>
        [Fact]
        public async Task ClosedConnectionReconnectsForNextQuery() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var firstClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int accepted = 0;
            Task server = Task.Run(async () => {
                using (TcpClient firstConnection = await AcceptAsync(listener, timeout.Token)) {
                    Interlocked.Increment(ref accepted);
                    NetworkStream stream = firstConnection.GetStream();
                    byte[] query = await ReadFrameAsync(stream, timeout.Token);
                    await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(query), timeout.Token);
                }
                firstClosed.TrySetResult(true);
                using TcpClient secondConnection = await AcceptAsync(listener, timeout.Token);
                Interlocked.Increment(ref accepted);
                NetworkStream secondStream = secondConnection.GetStream();
                byte[] secondQuery = await ReadFrameAsync(secondStream, timeout.Token);
                await WriteFrameAsync(secondStream, TestUtilities.CreateResponseFromQuery(secondQuery), timeout.Token);
            }, timeout.Token);

            try {
                var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                    Port = port,
                    TimeOut = 2000
                };
                using var client = new ClientX(configuration);
                DnsResponse first = await client.Resolve("first.example", retryOnTransient: false,
                    cancellationToken: timeout.Token);
                await firstClosed.Task;
                await Task.Delay(50, timeout.Token);
                DnsResponse second = await client.Resolve("second.example", retryOnTransient: false,
                    cancellationToken: timeout.Token);
                await server;

                Assert.Equal(DnsResponseCode.NoError, first.Status);
                Assert.Equal(DnsResponseCode.NoError, second.Status);
                Assert.Equal(2, Volatile.Read(ref accepted));
            } finally {
                listener.Stop();
            }
        }

#if NET6_0_OR_GREATER
        /// <summary>DoT uses the same pipelined response-correlation engine over one authenticated stream.</summary>
        [Fact]
        public async Task DotPipeliningDispatchesOutOfOrderResponses() {
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            using X509Certificate2 baseCertificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            byte[] pfx = baseCertificate.Export(X509ContentType.Pfx);
#if NET9_0_OR_GREATER
            using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12(
                pfx, null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet,
                Pkcs12LoaderLimits.Defaults);
#else
            using X509Certificate2 certificate = new X509Certificate2(pfx, (string?)null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
#endif
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Task server = RunOutOfOrderServerAsync(listener, useTls: true, certificate, timeout.Token);

            try {
                var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) {
                    Port = port,
                    TimeOut = 3000,
                    TlsServerName = "localhost"
                };
                using var client = new ClientX(configuration, ignoreCertificateErrors: true);
                Task<DnsResponse> firstTask = client.Resolve("first.example", retryOnTransient: false,
                    cancellationToken: timeout.Token);
                Task<DnsResponse> secondTask = client.Resolve("second.example", retryOnTransient: false,
                    cancellationToken: timeout.Token);

                DnsResponse[] responses = await Task.WhenAll(firstTask, secondTask);
                await server;

                Assert.Equal("first.example", Assert.Single(responses[0].Questions).Name);
                Assert.Equal("second.example", Assert.Single(responses[1].Questions).Name);
            } finally {
                listener.Stop();
            }
        }
#endif

        private static async Task RunOutOfOrderServerAsync(TcpListener listener, bool useTls,
            X509Certificate2? certificate, CancellationToken cancellationToken) {
            using TcpClient connection = await AcceptAsync(listener, cancellationToken);
            Stream stream = connection.GetStream();
            SslStream? tls = null;
            try {
                if (useTls) {
                    tls = new SslStream(stream, false);
#if NET6_0_OR_GREATER
                    SslProtocols protocols = SslProtocols.Tls12 | SslProtocols.Tls13;
#else
                    SslProtocols protocols = SslProtocols.Tls12;
#endif
                    await tls.AuthenticateAsServerAsync(certificate!, false,
                        protocols, false);
                    stream = tls;
                }
                var queries = new List<byte[]> {
                    await ReadFrameAsync(stream, cancellationToken),
                    await ReadFrameAsync(stream, cancellationToken)
                };
                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(queries[1]), cancellationToken);
                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(queries[0]), cancellationToken);
            } finally {
                tls?.Dispose();
            }
        }

        private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken) {
            var length = new byte[2];
            await TestUtilities.ReadExactlyAsync(stream, length, length.Length, cancellationToken);
            int count = (length[0] << 8) | length[1];
            var message = new byte[count];
            await TestUtilities.ReadExactlyAsync(stream, message, message.Length, cancellationToken);
            return message;
        }

        private static async Task WriteFrameAsync(Stream stream, byte[] message,
            CancellationToken cancellationToken) {
            byte[] length = { (byte)(message.Length >> 8), (byte)message.Length };
            await stream.WriteAsync(length, 0, length.Length, cancellationToken);
            await stream.WriteAsync(message, 0, message.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static async Task<string> GetQuestionNameAsync(byte[] message) {
            DnsResponse parsed = await DnsWire.DeserializeDnsWireFormat(null, false, message);
            return Assert.Single(parsed.Questions).Name;
        }

        private static async Task<TcpClient> AcceptAsync(TcpListener listener,
            CancellationToken cancellationToken) {
#if NET8_0_OR_GREATER
            return await listener.AcceptTcpClientAsync(cancellationToken);
#else
            Task<TcpClient> accept = listener.AcceptTcpClientAsync();
            Task completed = await Task.WhenAny(accept, Task.Delay(Timeout.Infinite, cancellationToken));
            if (completed != accept) throw new OperationCanceledException(cancellationToken);
            return await accept;
#endif
        }
    }
}
