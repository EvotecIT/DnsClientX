using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public partial class DnsStreamConnectionPoolTests {
        /// <summary>A fully received reply survives a late write error when its peer immediately closes.</summary>
        [Fact]
        public async Task CompletedTcpResponseSurvivesLateWriteFailure() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Task server = Task.Run(async () => {
                try {
                    for (int attempt = 0; attempt < 2; attempt++) {
                        using TcpClient connection = await AcceptAsync(listener, guard.Token);
                        NetworkStream stream = connection.GetStream();
                        byte[] query = await ReadFrameAsync(stream, guard.Token);
                        await WriteFrameAsync(stream, TestUtilities.CreateResponseFromQuery(query), guard.Token);
                    }
                } catch (OperationCanceledException) when (guard.IsCancellationRequested) {
                } catch (SocketException) when (guard.IsCancellationRequested) {
                } catch (ObjectDisposedException) when (guard.IsCancellationRequested) {
                }
            }, guard.Token);
            try {
                using var pool = new DnsStreamConnectionPool(writeOverride: async (stream, frame, token) => {
                    await stream.WriteAsync(frame, 0, frame.Length, token);
                    // The reader completes the response before EOF retires and disposes the stream.
                    // Hold the writer at that real boundary to make the response/close race deterministic.
                    while (stream.CanWrite) {
                        token.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }
                    throw new IOException("The peer closed while the completed write was unwinding.");
                });
                byte[] query = new DnsMessage("completed.example", DnsRecordType.A,
                    new DnsMessageOptions(TransactionId: 0x4567)).SerializeDnsWireFormat();
                byte[] response = await pool.QueryTcpAsync(IPAddress.Loopback, port, null,
                    query, 5000, 1, guard.Token);
                Assert.Equal("completed.example", await GetQuestionNameAsync(response));
            } finally {
                guard.Cancel();
                listener.Stop();
                await server;
            }
        }
    }
}
