using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared single-target query and update workflows.
    /// </summary>
    [Collection("NoParallel")]
    public class ResolverSingleOperationWorkflowTests {
        private sealed class UpdateServer {
            public UpdateServer(int port, Task task) {
                Port = port;
                Task = task;
            }

            public int Port { get; }
            public Task Task { get; }
        }

        private static async Task RunUdpServerAsync(int port, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
#if NET5_0_OR_GREATER
            UdpReceiveResult result = await udp.ReceiveAsync(token);
            byte[] response = TestUtilities.CreateResponseFromQuery(result.Buffer);
            await udp.SendAsync(response, result.RemoteEndPoint, token);
#else
            var receiveTask = udp.ReceiveAsync();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));
            if (completed != receiveTask) {
                throw new OperationCanceledException(token);
            }

            UdpReceiveResult result = receiveTask.Result;
            byte[] response = TestUtilities.CreateResponseFromQuery(result.Buffer);
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
#endif
        }

        private static void WriteUInt16(Stream stream, ushort value) {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static byte[] EncodeName(string name) {
            name = name.TrimEnd('.');
            using var ms = new MemoryStream();
            foreach (string part in name.Split('.')) {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(part);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }

            ms.WriteByte(0);
            return ms.ToArray();
        }

        private static UpdateServer RunUpdateServerAsync(DnsResponseCode code, CancellationToken token) {
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
                await TestUtilities.ReadExactlyAsync(stream, len, 2, token);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(len);
                }

                int queryLength = BitConverter.ToUInt16(len, 0);
                byte[] query = new byte[queryLength];
                await TestUtilities.ReadExactlyAsync(stream, query, queryLength, token);
                byte[] response = TestUtilities.CreateResponseFromQuery(query, (ushort)(0xA800 | (ushort)code));

                byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(prefix);
                }

                await stream.WriteAsync(prefix, 0, prefix.Length, token);
                await stream.WriteAsync(response, 0, response.Length, token);
                listener.Stop();
            }

            return new UpdateServer(port, Serve());
        }

        /// <summary>
        /// Ensures query workflows produce explain-ready metadata.
        /// </summary>
        [Fact]
        public async Task QueryAsync_ReturnsOperationMetadata() {
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunUdpServerAsync(port, cts.Token);

            ResolverSingleOperationResult result = await ResolverSingleOperationWorkflow.QueryAsync(
                new ResolverExecutionTarget {
                    DisplayName = $"udp@127.0.0.1:{port}",
                    ExplicitEndpoint = new DnsResolverEndpoint {
                        Transport = Transport.Udp,
                        Host = "127.0.0.1",
                        Port = port
                    }
                },
                "example.com",
                DnsRecordType.A,
                clientOptions: new ResolverExecutionClientOptions {
                    EnableAudit = true
                },
                cancellationToken: cts.Token);

            await serverTask;
            Assert.Equal(DnsResponseCode.NoError, result.Response.Status);
            Assert.Equal(DnsRequestFormat.DnsOverUDP, result.RequestFormat);
            Assert.Equal(port, result.ConfiguredResolverPort);
            Assert.Single(result.AuditTrail);
        }

        /// <summary>Saved endpoint DNSSEC settings survive normal selection; an explicit request can also enable DO.</summary>
        [Theory]
        [InlineData(true, false, true)]
        [InlineData(false, false, false)]
        [InlineData(null, false, false)]
        [InlineData(false, true, true)]
        public async Task QueryAsync_SavedEndpointPreservesDnsSecDoBit(bool? endpointDnsSec, bool requestDnsSec, bool expectedDo) {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            Task<byte[][]> serverTask = CaptureProbeAndQueryAsync(udp, cts.Token);
            string path = Path.GetTempFileName();
            try {
                var endpoint = new DnsResolverEndpoint { Host = "127.0.0.1", Port = port, Transport = Transport.Udp, DnsSecOk = endpointDnsSec };
                ResolverProbeReport probe = await ResolverProbeWorkflow.RunAsync(
                    ResolverExecutionPlanBuilder.BuildExplicitTargets(new[] { endpoint }), "example.com", DnsRecordType.A, 2000,
                    new ResolverQueryRunOptions(), new ResolverProbePolicy(), cancellationToken: cts.Token);
                ResolverScoreStore.Save(path, probe.Snapshot);
                ResolverSingleOperationResult result = await ResolverSingleOperationWorkflow.QueryAsync(
                    new ResolverExecutionTargetSource { ResolverSelectionPath = path }, "example.com", DnsRecordType.A,
                    requestDnsSec: requestDnsSec, cancellationToken: cts.Token);
                byte[][] queries = await serverTask;
                Assert.Equal(DnsResponseCode.NoError, result.Response.Status);
                Assert.Equal(endpointDnsSec == true, HasDoBit(queries[0]));
                Assert.Equal(expectedDo, HasDoBit(queries[1]));
                Assert.Equal(endpointDnsSec, endpoint.DnsSecOk);
            } finally { cts.Cancel(); File.Delete(path); }
        }

        private static async Task<byte[][]> CaptureProbeAndQueryAsync(UdpClient udp, CancellationToken token) {
            var queries = new byte[2][];
            for (int index = 0; index < queries.Length; index++) {
#if NET5_0_OR_GREATER
                UdpReceiveResult received = await udp.ReceiveAsync(token);
#else
                Task<UdpReceiveResult> receive = udp.ReceiveAsync();
                if (await Task.WhenAny(receive, Task.Delay(Timeout.Infinite, token)) != receive) throw new OperationCanceledException(token);
                UdpReceiveResult received = await receive;
#endif
                queries[index] = received.Buffer;
                byte[] response = TestUtilities.CreateResponseFromQuery(received.Buffer);
                await udp.SendAsync(response, response.Length, received.RemoteEndPoint);
            }
            return queries;
        }

        private static bool HasDoBit(byte[] query) {
            int additionalCount = (query[10] << 8) | query[11];
            if (additionalCount == 0) return false;
            Assert.Equal(1, additionalCount);
            int offset = 12;
            while (query[offset] != 0) offset += 1 + query[offset];
            offset += 5; // Question terminator, type, and class.
            Assert.Equal(0, query[offset]);
            Assert.Equal((ushort)DnsRecordType.OPT, (ushort)((query[offset + 1] << 8) | query[offset + 2]));
            return (query[offset + 7] & 0x80) != 0;
        }

        /// <summary>
        /// Ensures update workflows return response and resolver metadata.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_ReturnsOperationMetadata() {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            UpdateServer server = RunUpdateServerAsync(DnsResponseCode.NoError, cts.Token);

            ResolverSingleOperationResult result = await ResolverSingleOperationWorkflow.UpdateAsync(
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
            Assert.Equal(DnsResponseCode.NoError, result.Response.Status);
            Assert.Equal(DnsRequestFormat.DnsOverTCP, result.RequestFormat);
            Assert.Equal(server.Port, result.ConfiguredResolverPort);
            Assert.True(result.Elapsed >= TimeSpan.Zero);
        }

#if NET472
        /// <summary>
        /// Ensures unsupported modern transports return a shared non-network NotImplemented result on older frameworks.
        /// </summary>
        [Fact]
        public async Task QueryAsync_UnsupportedModernTransport_ReturnsNotImplementedWithoutAuditEntries() {
            ResolverSingleOperationResult result = await ResolverSingleOperationWorkflow.QueryAsync(
                new ResolverExecutionTarget {
                    DisplayName = "doh3@dns.quad9.net:443",
                    ExplicitEndpoint = new DnsResolverEndpoint {
                        Transport = Transport.Doh,
                        Host = "dns.quad9.net",
                        Port = 443,
                        RequestFormat = DnsRequestFormat.DnsOverHttp3
                    }
                },
                "example.com",
                DnsRecordType.A);

            Assert.Equal(DnsResponseCode.NotImplemented, result.Response.Status);
            Assert.Equal(DnsRequestFormat.DnsOverHttp3, result.RequestFormat);
            Assert.Empty(result.AuditTrail);
            Assert.Contains("net8+", result.Response.Error, StringComparison.OrdinalIgnoreCase);
        }
#endif
    }
}
