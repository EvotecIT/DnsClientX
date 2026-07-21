using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        /// <summary>A saturated RFC 7766 connection cannot hide an unbounded wait before the query deadline starts.</summary>
        [Fact]
        public Task CapacityWaitUsesWholeQueryDeadline() =>
            AssertQueuedQueryUsesWholeDeadline(sameTransactionId: false, maxInFlight: 1);

        /// <summary>A colliding transaction ID cannot wait beyond the second caller's whole-query deadline.</summary>
        [Fact]
        public Task TransactionIdWaitUsesWholeQueryDeadline() =>
            AssertQueuedQueryUsesWholeDeadline(sameTransactionId: true, maxInFlight: 2);

        /// <summary>Caller cancellation during connection establishment is not disguised as a transport failure.</summary>
        [Fact]
        public async Task CallerCancellationDuringConnectRemainsOperationCanceled() {
            using var cancellation = new CancellationTokenSource();
            var connectStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var pool = new DnsStreamConnectionPool(connectOverride: async (_, _, _, _, token) => {
                connectStarted.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
            });
            byte[] query = new DnsMessage("connect.example", DnsRecordType.A,
                new DnsMessageOptions(TransactionId: 0x1234)).SerializeDnsWireFormat();

            Task<byte[]> pending = pool.QueryTcpAsync(IPAddress.Loopback, 53, null,
                query, 5000, 1, cancellation.Token);
            await connectStarted.Task;
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }

        /// <summary>Caller cancellation during a framed write is not disguised as a transport failure.</summary>
        [Fact]
        public async Task CallerCancellationDuringWriteRemainsOperationCanceled() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var cancellation = new CancellationTokenSource();
            var writeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<TcpClient> accept = AcceptAsync(listener, guard.Token);

            try {
                using var pool = new DnsStreamConnectionPool(writeOverride: async (_, _, token) => {
                    writeStarted.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, token);
                });
                byte[] query = new DnsMessage("write.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 0x5678)).SerializeDnsWireFormat();
                Task<byte[]> pending = pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    query, 5000, 1, cancellation.Token);

                using TcpClient accepted = await accept;
                await writeStarted.Task;
                cancellation.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            } finally {
                listener.Stop();
            }
        }

        /// <summary>A canceled request keeps its transaction ID reserved until the late response is drained.</summary>
        [Fact]
        public async Task CancelledTransactionIdIsNotReusedBeforeLateResponse() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var firstReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseLateResponse = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task server = Task.Run(async () => {
                using TcpClient connection = await AcceptAsync(listener, guard.Token);
                NetworkStream stream = connection.GetStream();
                byte[] firstQuery = await ReadFrameAsync(stream, guard.Token);
                firstReceived.TrySetResult(true);

                Task<byte[]> secondRead = ReadFrameAsync(stream, guard.Token);
                await releaseLateResponse.Task;
                Assert.False(secondRead.IsCompleted,
                    "The canceled transaction ID was reused before its late response was drained.");
                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(firstQuery), guard.Token);

                byte[] secondQuery = await secondRead;
                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(secondQuery), guard.Token);
            }, guard.Token);

            try {
                using var pool = new DnsStreamConnectionPool();
                byte[] firstQuery = new DnsMessage("old.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 0x4242)).SerializeDnsWireFormat();
                byte[] secondQuery = new DnsMessage("new.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 0x4242)).SerializeDnsWireFormat();
                using var cancellation = new CancellationTokenSource();
                Task<byte[]> first = pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    firstQuery, 5000, 2, cancellation.Token);
                await firstReceived.Task;
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

                Task<byte[]> second = pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    secondQuery, 5000, 2, guard.Token);
                await Task.Delay(100, guard.Token);
                releaseLateResponse.TrySetResult(true);

                byte[] response = await second;
                DnsResponse parsed = await DnsWire.DeserializeDnsWireFormat(null, false, response);
                Assert.Equal("new.example", Assert.Single(parsed.Questions).Name);
                await server;
            } finally {
                releaseLateResponse.TrySetResult(true);
                listener.Stop();
            }
        }

        /// <summary>A timed-out written request keeps its connection capacity until the late reply is drained.</summary>
        [Fact]
        public async Task TimedOutWrittenQueryKeepsCapacityUntilLateResponse() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var firstReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseLateResponse = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task server = Task.Run(async () => {
                using TcpClient connection = await AcceptAsync(listener, guard.Token);
                NetworkStream stream = connection.GetStream();
                byte[] firstQuery = await ReadFrameAsync(stream, guard.Token);
                firstReceived.TrySetResult(true);

                Task<byte[]> secondRead = ReadFrameAsync(stream, guard.Token);
                await releaseLateResponse.Task;
                Assert.False(secondRead.IsCompleted,
                    "A timed-out request released stream capacity before its late response was drained.");
                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(firstQuery), guard.Token);

                byte[] secondQuery = await secondRead;
                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(secondQuery), guard.Token);
            }, guard.Token);

            try {
                using var pool = new DnsStreamConnectionPool();
                byte[] firstQuery = new DnsMessage("old.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 0x4242)).SerializeDnsWireFormat();
                byte[] secondQuery = new DnsMessage("new.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 0x4343)).SerializeDnsWireFormat();

                Task<byte[]> first = pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    firstQuery, 150, 1, guard.Token);
                await firstReceived.Task;
                await Assert.ThrowsAsync<TimeoutException>(() => first);

                Task<byte[]> second = pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    secondQuery, 5000, 1, guard.Token);
                await Task.Delay(100, guard.Token);
                releaseLateResponse.TrySetResult(true);

                byte[] response = await second;
                DnsResponse parsed = await DnsWire.DeserializeDnsWireFormat(null, false, response);
                Assert.Equal("new.example", Assert.Single(parsed.Questions).Name);
                await server;
            } finally {
                releaseLateResponse.TrySetResult(true);
                listener.Stop();
            }
        }

        /// <summary>Canceling behind the write gate leaves already-sent work and the shared stream intact.</summary>
        [Fact]
        public async Task QueuedWriteCancellationDoesNotFaultSharedConnection() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var firstWriteStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int accepted = 0;
            int writes = 0;
            Task server = Task.Run(async () => {
                using TcpClient connection = await AcceptAsync(listener, guard.Token);
                Interlocked.Increment(ref accepted);
                NetworkStream stream = connection.GetStream();
                for (int index = 0; index < 2; index++) {
                    byte[] query = await ReadFrameAsync(stream, guard.Token);
                    await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(query), guard.Token);
                }
            }, guard.Token);

            try {
                using var pool = new DnsStreamConnectionPool(writeOverride: async (stream, frame, token) => {
                    if (Interlocked.Increment(ref writes) == 1) {
                        firstWriteStarted.TrySetResult(true);
                        await WaitForSignalAsync(releaseFirstWrite.Task, token);
                    }
                    await stream.WriteAsync(frame, 0, frame.Length, token);
                    await stream.FlushAsync(token);
                });
                byte[] firstQuery = new DnsMessage("first.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 0x1001)).SerializeDnsWireFormat();
                byte[] canceledQuery = new DnsMessage("cancel.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 0x1002)).SerializeDnsWireFormat();
                byte[] thirdQuery = new DnsMessage("third.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 0x1003)).SerializeDnsWireFormat();

                Task<byte[]> first = pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    firstQuery, 5000, 3, guard.Token);
                await firstWriteStarted.Task;
                using var cancellation = new CancellationTokenSource();
                Task<byte[]> canceled = pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    canceledQuery, 5000, 3, cancellation.Token);
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);

                releaseFirstWrite.TrySetResult(true);
                await first;
                byte[] thirdResponse = await pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    thirdQuery, 5000, 3, guard.Token);
                await server;

                DnsResponse parsed = await DnsWire.DeserializeDnsWireFormat(null, false, thirdResponse);
                Assert.Equal("third.example", Assert.Single(parsed.Questions).Name);
                Assert.Equal(1, Volatile.Read(ref accepted));
                Assert.Equal(2, Volatile.Read(ref writes));
            } finally {
                releaseFirstWrite.TrySetResult(true);
                listener.Stop();
            }
        }

        /// <summary>Concurrent retries replace a failed connection once without disposing the fresh replacement.</summary>
        [Fact]
        public async Task ConcurrentRetryKeepsFreshReplacementConnection() {
            const int QueryCount = 12;
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            int accepted = 0;
            Task server = Task.Run(async () => {
                using (TcpClient failed = await AcceptAsync(listener, guard.Token)) {
                    Interlocked.Increment(ref accepted);
                    NetworkStream stream = failed.GetStream();
                    for (int index = 0; index < QueryCount; index++) {
                        await ReadFrameAsync(stream, guard.Token);
                    }
                }

                using TcpClient replacement = await AcceptAsync(listener, guard.Token);
                Interlocked.Increment(ref accepted);
                NetworkStream replacementStream = replacement.GetStream();
                for (int index = 0; index < QueryCount; index++) {
                    byte[] query = await ReadFrameAsync(replacementStream, guard.Token);
                    await WriteFrameAsync(replacementStream, TestUtilities.CreateResponseFromQuery(query), guard.Token);
                }
            }, guard.Token);

            try {
                using var pool = new DnsStreamConnectionPool();
                var queries = new Task<byte[]>[QueryCount];
                for (int index = 0; index < QueryCount; index++) {
                    byte[] query = new DnsMessage($"retry-{index}.example", DnsRecordType.A,
                        new DnsMessageOptions(TransactionId: checked((ushort)(0x2000 + index))))
                        .SerializeDnsWireFormat();
                    queries[index] = pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                        query, 10000, QueryCount, guard.Token);
                }

                byte[][] responses = await Task.WhenAll(queries);
                await server;

                Assert.Equal(QueryCount, responses.Length);
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

        private static async Task AssertQueuedQueryUsesWholeDeadline(bool sameTransactionId, int maxInFlight) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var firstReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task server = Task.Run(async () => {
                using TcpClient connection = await AcceptAsync(listener, guard.Token);
                NetworkStream stream = connection.GetStream();
                byte[] query = await ReadFrameAsync(stream, guard.Token);
                firstReceived.TrySetResult(true);
                await releaseFirst.Task;
                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(query), guard.Token);
            }, guard.Token);

            try {
                using var pool = new DnsStreamConnectionPool();
                byte[] firstQuery = new DnsMessage("first.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 0x1234)).SerializeDnsWireFormat();
                byte[] secondQuery = new DnsMessage("second.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: sameTransactionId ? (ushort)0x1234 : (ushort)0x5678))
                    .SerializeDnsWireFormat();
                Task<byte[]> first = pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    firstQuery, 2000, maxInFlight, guard.Token);
                await firstReceived.Task;

                var elapsed = Stopwatch.StartNew();
                Task<byte[]> queued = pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    secondQuery, 150, maxInFlight, guard.Token);
                await Assert.ThrowsAsync<TimeoutException>(() => queued);
                elapsed.Stop();

                Assert.InRange(elapsed.ElapsedMilliseconds, 50, 1500);
                releaseFirst.TrySetResult(true);
                byte[] response = await first;
                Assert.Equal((byte)0x12, response[0]);
                Assert.Equal((byte)0x34, response[1]);
                await server;
            } finally {
                releaseFirst.TrySetResult(true);
                listener.Stop();
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

        private static async Task WaitForSignalAsync(Task signal, CancellationToken cancellationToken) {
            Task completed = await Task.WhenAny(signal, Task.Delay(Timeout.Infinite, cancellationToken));
            if (completed != signal) throw new OperationCanceledException(cancellationToken);
            await signal;
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
