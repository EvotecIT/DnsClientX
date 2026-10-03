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

#if NET6_0_OR_GREATER
        /// <summary>The shared stream contract also survives a permanently missing DoT reply.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LostTlsReplyRecoversWithOneSlot(bool sameId) {
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            await AssertLostReplyRecoversAsync(sameId, cancel: false, useTls: true, certificate);
        }
#endif

        private static async Task AssertLostReplyRecoversAsync(bool sameId, bool cancel, bool useTls, X509Certificate2? certificate) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var cancellation = new CancellationTokenSource();
            var receivedFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int queries = 0;
            Task server = Task.Run(async () => {
                // Different IDs can use the same connection immediately. The same ID must
                // wait for bounded retirement and use a new stream, since its old reply is lost.
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
                Task<byte[]> missing = Query(first, 500, cancel ? cancellation.Token : guard.Token);
                await WaitForSignalAsync(receivedFirst.Task, guard.Token);
                if (cancel) {
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => missing);
                } else {
                    await Assert.ThrowsAsync<TimeoutException>(() => missing);
                }
                byte[] response = await Query(second, 3000, guard.Token);
                Assert.Equal("next.example", await GetQuestionNameAsync(response));
                await server;
                Assert.Equal(2, Volatile.Read(ref queries));
            } finally {
                guard.Cancel(); listener.Stop();
            }
        }
    }
}
