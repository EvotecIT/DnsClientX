using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared resolver update workflows.
    /// </summary>
    public class ResolverUpdateWorkflowTests {
        private sealed class UpdateServer {
            public UpdateServer(int port, Task task) {
                Port = port;
                Task = task;
            }

            public int Port { get; }
            public Task Task { get; }
        }

        private static void WriteUInt16(System.IO.Stream stream, ushort value) {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static byte[] EncodeName(string name) {
            name = name.TrimEnd('.');
            using var ms = new System.IO.MemoryStream();
            foreach (string part in name.Split('.')) {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(part);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }

            ms.WriteByte(0);
            return ms.ToArray();
        }

        private static byte[] BuildResponse(DnsResponseCode code) {
            using var ms = new System.IO.MemoryStream();
            WriteUInt16(ms, 1);
            ushort flags = (ushort)(0x8000 | (ushort)code);
            WriteUInt16(ms, flags);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);
            byte[] zone = EncodeName("example.com");
            ms.Write(zone, 0, zone.Length);
            WriteUInt16(ms, (ushort)DnsRecordType.SOA);
            WriteUInt16(ms, 1);
            return ms.ToArray();
        }

        private static UpdateServer RunServerAsync(byte[] response, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            async Task Serve() {
#if NETFRAMEWORK
                using TcpClient client = await listener.AcceptTcpClientAsync();
#else
                using TcpClient client = await listener.AcceptTcpClientAsync(token);
#endif
                using NetworkStream stream = client.GetStream();
                byte[] len = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, len, 2, token).ConfigureAwait(false);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(len);
                }

                int queryLength = BitConverter.ToUInt16(len, 0);
                byte[] query = new byte[queryLength];
                await TestUtilities.ReadExactlyAsync(stream, query, queryLength, token).ConfigureAwait(false);

                byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(prefix);
                }

                await stream.WriteAsync(prefix, 0, prefix.Length, token).ConfigureAwait(false);
                await stream.WriteAsync(response, 0, response.Length, token).ConfigureAwait(false);
                listener.Stop();
            }

            return new UpdateServer(port, Serve());
        }

        /// <summary>
        /// Ensures shared update workflows execute DNS update requests against explicit targets.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_ExecutesAgainstExplicitTarget() {
            byte[] response = BuildResponse(DnsResponseCode.NoError);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            UpdateServer server = RunServerAsync(response, cts.Token);

            DnsResponse result = await ResolverUpdateWorkflow.UpdateAsync(
                new ResolverExecutionTarget {
                    DisplayName = $"tcp@127.0.0.1:{server.Port}",
                    ExplicitEndpoint = new DnsResolverEndpoint {
                        Transport = Transport.Tcp,
                        Host = "127.0.0.1",
                        Port = server.Port
                    }
                },
                "example.com",
                "www.example.com",
                DnsRecordType.A,
                "1.2.3.4",
                cancellationToken: cts.Token);

            await server.Task;
            Assert.Equal(DnsResponseCode.NoError, result.Status);
        }
    }
}
