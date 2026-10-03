using System;
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
    public partial class DnsStreamConnectionPoolTests {
        /// <summary>A peer omitting a reply cannot permanently consume the only slot or transaction ID.</summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public Task LostTcpReplyRecoversWithOneSlot(bool sameId, bool cancel) =>
            AssertLostReplyRecoversAsync(sameId, cancel, useTls: false, certificate: null);

        /// <summary>Repeated post-write cancellations cannot grow unanswered work beyond the configured cap.</summary>
        [Fact]
        public async Task CanceledBurstBoundsOutstandingQueriesOnEachConnection() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var stop = guard.Token.Register(listener.Stop);
            var received = new TaskCompletionSource<bool>[6];
            for (int i = 0; i < received.Length; i++) received[i] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int queries = 0, connections = 0, maximum = 0;
            Task server = Task.Run(async () => {
                while (queries < 7) {
                    using TcpClient connection = await AcceptAsync(listener, guard.Token);
                    connections++;
                    var stream = connection.GetStream();
                    int onConnection = 0;
                    try {
                        while (true) {
                            byte[] query = await ReadFrameAsync(stream, guard.Token);
                            maximum = Math.Max(maximum, ++onConnection);
                            if (++queries <= received.Length) received[queries - 1].TrySetResult(true);
                            else {
                                await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(query), guard.Token);
                                return;
                            }
                        }
                    } catch (IOException) when (queries <= received.Length) { }
                }
            }, guard.Token);
            try {
                using var pool = new DnsStreamConnectionPool();
                for (int i = 0; i < received.Length; i++) {
                    byte[] query = new DnsMessage("canceled.example", DnsRecordType.A,
                        new DnsMessageOptions(TransactionId: (ushort)(100 + i))).SerializeDnsWireFormat();
                    using var cancellation = new CancellationTokenSource();
                    Task<byte[]> response = pool.QueryTcpAsync(IPAddress.Loopback, port, null, query, 3000, 2, cancellation.Token);
                    await WaitForSignalAsync(received[i].Task, guard.Token);
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response);
                }
                byte[] final = new DnsMessage("final.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 106)).SerializeDnsWireFormat();
                Assert.Equal("final.example", await GetQuestionNameAsync(await pool.QueryTcpAsync(
                    IPAddress.Loopback, port, null, final, 3000, 2, guard.Token)));
                await server;
                Assert.InRange(maximum, 1, 2);
                Assert.Equal(4, connections);
            } finally { guard.Cancel(); listener.Stop(); }
        }

#if NET6_0_OR_GREATER
        /// <summary>The shared stream contract also survives a permanently missing DoT reply.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LostTlsReplyRecoversWithOneSlot(bool sameId) {
            using X509Certificate2 certificate = CreateStreamServerCertificate();
            await AssertLostReplyRecoversAsync(sameId, cancel: false, useTls: true, certificate);
        }
#endif

        private static async Task AssertLostReplyRecoversAsync(bool sameId, bool cancel, bool useTls, X509Certificate2? certificate) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var cancellation = new CancellationTokenSource();
            var receivedFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int queries = 0;
            Task server = Task.Run(async () => {
                // The unanswered request occupies the only in-flight reservation. Both IDs
                // must use a new stream without spending the next query's deadline draining it.
                while (Volatile.Read(ref queries) < 2) {
                    using TcpClient connection = await AcceptAsync(listener, guard.Token);
                    Stream stream = connection.GetStream();
                    using var tls = useTls ? new SslStream(stream, false) : null;
                    if (tls != null) {
                        await tls.AuthenticateAsServerAsync(certificate!, false, SslProtocols.Tls12, false);
                        stream = tls;
                    }
                    try {
                        while (true) {
                            byte[] query = await ReadFrameAsync(stream, guard.Token);
                            if (Interlocked.Increment(ref queries) == 1) {
                                receivedFirst.TrySetResult(true);
                                continue;
                            }
                            await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(query), guard.Token);
                            return;
                        }
                    } catch (IOException) when (Volatile.Read(ref queries) == 1) {
                        // Expected EOF when the abandoned ID cannot be drained.
                    }
                }
            }, guard.Token);
            try {
                using var pool = new DnsStreamConnectionPool();
                byte[] first = new DnsMessage("lost.example", DnsRecordType.A, new DnsMessageOptions(TransactionId: 41)).SerializeDnsWireFormat();
                byte[] second = new DnsMessage("next.example", DnsRecordType.A, new DnsMessageOptions(TransactionId: sameId ? (ushort)41 : (ushort)42)).SerializeDnsWireFormat();
                Task<byte[]> Query(byte[] wire, int timeout, CancellationToken token) => useTls
                    ? pool.QueryTlsAsync(IPAddress.Loopback, port, null, "localhost", true, SslProtocols.Tls12, wire, timeout, 1, token)
                    : pool.QueryTcpAsync(IPAddress.Loopback, port, null, wire, timeout, 1, token);
                const int queryTimeout = 3000; // Includes the initial TLS handshake on every platform.
                Task<byte[]> missing = Query(first, queryTimeout, cancel ? cancellation.Token : guard.Token);
                // Surface handshake or query faults directly instead of masking them
                // behind the server's first-frame signal and the outer guard.
                Task firstEvent = await Task.WhenAny(receivedFirst.Task, missing, server);
                if (firstEvent != receivedFirst.Task) await firstEvent;
                await WaitForSignalAsync(receivedFirst.Task, guard.Token);
                if (cancel) {
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => missing);
                } else {
                    await Assert.ThrowsAsync<TimeoutException>(() => missing);
                }
                byte[] response = await Query(second, queryTimeout, guard.Token);
                Assert.Equal("next.example", await GetQuestionNameAsync(response));
                await server;
                Assert.Equal(2, Volatile.Read(ref queries));
            } finally {
                guard.Cancel(); listener.Stop();
            }
        }
    }
}
