using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for AXFR zone transfers under various scenarios.
    /// </summary>
    public class ZoneTransferTests {
        private static byte[] EncodeName(string name) {
            name = name.TrimEnd('.');
            var parts = name.Split('.');
            using var ms = new System.IO.MemoryStream();
            foreach (var part in parts) {
                ms.WriteByte((byte)part.Length);
                var bytes = System.Text.Encoding.ASCII.GetBytes(part);
                ms.Write(bytes, 0, bytes.Length);
            }
            ms.WriteByte(0);
            return ms.ToArray();
        }

        private static void WriteUInt16(System.IO.Stream s, ushort val) {
            var b = new byte[2];
            b[0] = (byte)(val >> 8);
            b[1] = (byte)val;
            s.Write(b, 0, 2);
        }

        private static void WriteUInt32(System.IO.Stream s, uint val) {
            var b = new byte[4];
            b[0] = (byte)(val >> 24);
            b[1] = (byte)(val >> 16);
            b[2] = (byte)(val >> 8);
            b[3] = (byte)val;
            s.Write(b, 0, 4);
        }

        private static byte[] BuildSoaRdata(uint serial = 1) {
            using var ms = new System.IO.MemoryStream();
            var nsBytes = EncodeName("ns1.example.com");
            ms.Write(nsBytes, 0, nsBytes.Length);
            var hostBytes = EncodeName("hostmaster.example.com");
            ms.Write(hostBytes, 0, hostBytes.Length);
            WriteUInt32(ms, serial);
            WriteUInt32(ms, 3600);
            WriteUInt32(ms, 600);
            WriteUInt32(ms, 86400);
            WriteUInt32(ms, 60);
            return ms.ToArray();
        }

        private static byte[] BuildMessage(string zone, params (string Name, DnsRecordType Type, byte[] Data)[] answers) {
            using var ms = new System.IO.MemoryStream();
            WriteUInt16(ms, 1); // id
            WriteUInt16(ms, 0x8400); // flags
            WriteUInt16(ms, 1); // qdcount
            WriteUInt16(ms, (ushort)answers.Length); // ancount
            WriteUInt16(ms, 0); // nscount
            WriteUInt16(ms, 0); // arcount
            var zoneBytes = EncodeName(zone);
            ms.Write(zoneBytes, 0, zoneBytes.Length);
            WriteUInt16(ms, (ushort)DnsRecordType.AXFR);
            WriteUInt16(ms, 1);
            foreach (var a in answers) {
                var nameBytes = EncodeName(a.Name);
                ms.Write(nameBytes, 0, nameBytes.Length);
                WriteUInt16(ms, (ushort)a.Type);
                WriteUInt16(ms, 1);
                WriteUInt32(ms, 3600);
                WriteUInt16(ms, (ushort)a.Data.Length);
                ms.Write(a.Data, 0, a.Data.Length);
            }
            return ms.ToArray();
        }

        private static byte[] BuildErrorMessage(string zone) {
            using var ms = new System.IO.MemoryStream();
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0x8405); // REFUSED
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);
            var zoneBytes = EncodeName(zone);
            ms.Write(zoneBytes, 0, zoneBytes.Length);
            WriteUInt16(ms, (ushort)DnsRecordType.AXFR);
            WriteUInt16(ms, 1);
            return ms.ToArray();
        }

        private sealed class AxfrServer {
            public int Port { get; }
            public Task Task { get; }

            public AxfrServer(int port, Task task) {
                Port = port;
                Task = task;
            }
        }

        private static AxfrServer RunAxfrServerAsync(byte[][] responses, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            async Task Serve() {
#if NETFRAMEWORK
                using TcpClient client = await listener.AcceptTcpClientAsync();
#else
                using TcpClient client = await listener.AcceptTcpClientAsync(token);
#endif
                NetworkStream stream = client.GetStream();
                byte[] len = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, len, 2, token);
                if (BitConverter.IsLittleEndian) Array.Reverse(len);
                int qLen = BitConverter.ToUInt16(len, 0);
                byte[] q = new byte[qLen];
                await TestUtilities.ReadExactlyAsync(stream, q, qLen, token);
                foreach (var r in responses) {
                    r[0] = q[0];
                    r[1] = q[1];
                    byte[] prefix = BitConverter.GetBytes((ushort)r.Length);
                    if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
                    await stream.WriteAsync(prefix, 0, prefix.Length, token);
                    await stream.WriteAsync(r, 0, r.Length, token);
                }
                listener.Stop();
            }

            return new AxfrServer(port, Serve());
        }

        /// <summary>
        /// Performs a successful zone transfer and returns records.
        /// </summary>
        [Fact]
        public async Task ZoneTransferAsync_ReturnsRecords() {
            var soa = BuildSoaRdata();
            byte[] m1 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            byte[] m2 = BuildMessage("example.com", ("www.example.com", DnsRecordType.A, new byte[] { 1, 2, 3, 4 }));
            byte[] m3 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(new[] { m1, m2, m3 }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            var recordSets = await client.ZoneTransferAsync("example.com");
            await server.Task;

            Assert.Equal(3, recordSets.Length);
            Assert.True(recordSets[0].IsOpening);
            Assert.Equal(DnsRecordType.SOA, recordSets[0].Records[0].Type);
            Assert.False(recordSets[1].IsOpening);
            Assert.Equal(DnsRecordType.A, recordSets[1].Records[0].Type);
            Assert.True(recordSets[2].IsClosing);
            Assert.Equal(DnsRecordType.SOA, recordSets[2].Records[0].Type);
        }

        /// <summary>
        /// Zone transfer should fail when the server returns an error.
        /// </summary>
        [Fact]
        public async Task ZoneTransferAsync_FailsWithError() {
            byte[] m1 = BuildErrorMessage("example.com");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(new[] { m1 }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            await Assert.ThrowsAsync<DnsClientException>(() => client.ZoneTransferAsync("example.com"));
            await server.Task;
        }

        /// <summary>
        /// Rejects a transfer whose first record is not the opening SOA.
        /// </summary>
        [Fact]
        public async Task ZoneTransferAsync_NoSoa_Throws() {
            byte[] m1 = BuildMessage("example.com", ("www.example.com", DnsRecordType.A, new byte[] { 1, 2, 3, 4 }));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(new[] { m1 }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            await Assert.ThrowsAsync<DnsClientException>(() => client.ZoneTransferAsync("example.com"));
            await server.Task;
        }

        /// <summary>
        /// Fails when the final SOA record is not present.
        /// </summary>
        [Fact]
        public async Task ZoneTransferAsync_FailsWithoutClosingSoa() {
            var soa = BuildSoaRdata();
            byte[] m1 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            byte[] m2 = BuildMessage("example.com", ("www.example.com", DnsRecordType.A, new byte[] { 1, 2, 3, 4 }));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(new[] { m1, m2 }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            await Assert.ThrowsAsync<DnsClientException>(() => client.ZoneTransferAsync("example.com"));
            await server.Task;
        }

        /// <summary>The opening SOA must belong to the zone named in the AXFR question.</summary>
        [Fact]
        public async Task ZoneTransferAsync_RejectsOpeningSoaForDifferentZone() {
            byte[] soa = BuildSoaRdata();
            byte[] response = BuildMessage("example.com",
                ("other.example", DnsRecordType.SOA, soa),
                ("other.example", DnsRecordType.SOA, soa));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(new[] { response }, cts.Token);
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                EndpointConfiguration = { Port = server.Port }
            };

            DnsClientException error = await Assert.ThrowsAsync<DnsClientException>(
                () => client.ZoneTransferAsync("example.com"));
            await server.Task;

            Assert.Contains("requested zone", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The closing SOA must be the same zone version as the opening SOA.</summary>
        [Fact]
        public async Task ZoneTransferAsync_RejectsMismatchedClosingSoa() {
            byte[] response = BuildMessage("example.com",
                ("example.com", DnsRecordType.SOA, BuildSoaRdata(1)),
                ("example.com", DnsRecordType.SOA, BuildSoaRdata(2)));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(new[] { response }, cts.Token);
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                EndpointConfiguration = { Port = server.Port }
            };

            DnsClientException error = await Assert.ThrowsAsync<DnsClientException>(
                () => client.ZoneTransferAsync("example.com"));
            await server.Task;

            Assert.Contains("closing SOA", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Fails if the closing SOA record is not the last record returned.
        /// </summary>
        [Fact]
        public async Task ZoneTransferAsync_FailsWhenClosingSoaNotLastRecord() {
            var soa = BuildSoaRdata();
            byte[] m1 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            byte[] m2 = BuildMessage(
                "example.com",
                ("example.com", DnsRecordType.SOA, soa),
                ("www.example.com", DnsRecordType.A, new byte[] { 1, 2, 3, 4 }));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(new[] { m1, m2 }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            await Assert.ThrowsAsync<DnsClientException>(() => client.ZoneTransferAsync("example.com"));
            await server.Task;
        }

        private static AxfrServer RunAxfrServerFailOnceAsync(byte[][] responses, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            async Task Serve() {
#if NETFRAMEWORK
                using (TcpClient client = await listener.AcceptTcpClientAsync()) {
#else
                using (TcpClient client = await listener.AcceptTcpClientAsync(token)) {
#endif
                    client.Close();
                }

#if NETFRAMEWORK
                using (TcpClient client = await listener.AcceptTcpClientAsync()) {
#else
                using (TcpClient client = await listener.AcceptTcpClientAsync(token)) {
#endif
                    NetworkStream stream = client.GetStream();
                    byte[] len = new byte[2];
                    await TestUtilities.ReadExactlyAsync(stream, len, 2, token);
                    if (BitConverter.IsLittleEndian) Array.Reverse(len);
                    int qLen = BitConverter.ToUInt16(len, 0);
                    byte[] q = new byte[qLen];
                    await TestUtilities.ReadExactlyAsync(stream, q, qLen, token);
                    foreach (var r in responses) {
                        r[0] = q[0];
                        r[1] = q[1];
                        byte[] prefix = BitConverter.GetBytes((ushort)r.Length);
                        if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
                        await stream.WriteAsync(prefix, 0, prefix.Length, token);
                        await stream.WriteAsync(r, 0, r.Length, token);
                    }
                }

                listener.Stop();
            }

            return new AxfrServer(port, Serve());
        }

        private static AxfrServer RunAxfrServerAlwaysFailAsync(int attempts, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            async Task Serve() {
                for (int i = 0; i < attempts; i++) {
#if NETFRAMEWORK
                    using TcpClient client = await listener.AcceptTcpClientAsync();
#else
                    using TcpClient client = await listener.AcceptTcpClientAsync(token);
#endif
                    client.Close();
                }

                listener.Stop();
            }

            return new AxfrServer(port, Serve());
        }

        private sealed class AxfrRetryProbeServer {
            internal AxfrRetryProbeServer(int port, Task<int> connections) {
                Port = port;
                Connections = connections;
            }
            internal int Port { get; }
            internal Task<int> Connections { get; }
        }

        private static AxfrRetryProbeServer RunPartialAxfrAndDetectRetryAsync(byte[] response,
            CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            async Task<int> Serve() {
                int connections = 1;
#if NETFRAMEWORK
                using (TcpClient client = await listener.AcceptTcpClientAsync()) {
#else
                using (TcpClient client = await listener.AcceptTcpClientAsync(token)) {
#endif
                    NetworkStream stream = client.GetStream();
                    byte[] len = new byte[2];
                    await TestUtilities.ReadExactlyAsync(stream, len, 2, token);
                    if (BitConverter.IsLittleEndian) Array.Reverse(len);
                    int queryLength = BitConverter.ToUInt16(len, 0);
                    byte[] query = new byte[queryLength];
                    await TestUtilities.ReadExactlyAsync(stream, query, queryLength, token);
                    response[0] = query[0];
                    response[1] = query[1];
                    byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
                    if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
                    await stream.WriteAsync(prefix, 0, prefix.Length, token);
                    await stream.WriteAsync(response, 0, response.Length, token);
                }

                await Task.Delay(300, token);
                if (listener.Pending()) {
                    connections++;
                    using TcpClient retry = await listener.AcceptTcpClientAsync();
                }
                listener.Stop();
                return connections;
            }
            return new AxfrRetryProbeServer(port, Serve());
        }

        /// <summary>
        /// Retries the zone transfer on transient failures.
        /// </summary>
        [Fact]
        public async Task ZoneTransferAsync_RetriesOnTransientFailure() {
            var soa = BuildSoaRdata();
            byte[] m1 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            byte[] m2 = BuildMessage("example.com", ("www.example.com", DnsRecordType.A, new byte[] { 1, 2, 3, 4 }));
            byte[] m3 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerFailOnceAsync(new[] { m1, m2, m3 }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            var recordSets = await client.ZoneTransferAsync("example.com", maxRetries: 2, retryDelayMs: 10);
            await server.Task;

            Assert.Equal(3, recordSets.Length);
        }

        /// <summary>
        /// Ensures the transfer fails after exceeding maximum retries.
        /// </summary>
        [Fact]
        public async Task ZoneTransferAsync_RetryFailsAfterMaxRetries() {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAlwaysFailAsync(2, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            await Assert.ThrowsAsync<DnsClientException>(() => client.ZoneTransferAsync("example.com", maxRetries: 2, retryDelayMs: 10));
            await server.Task;
        }

        /// <summary>A streamed prefix is never replayed by retrying a failed AXFR from the beginning.</summary>
        [Fact]
        public async Task ZoneTransferStreamAsync_DoesNotRetryAfterYieldingPrefix() {
            byte[] response = BuildMessage("example.com",
                ("example.com", DnsRecordType.SOA, BuildSoaRdata()),
                ("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 }));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            AxfrRetryProbeServer server = RunPartialAxfrAndDetectRetryAsync(response, cts.Token);
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                EndpointConfiguration = { Port = server.Port, TimeOut = 500 }
            };
            var yielded = new List<ZoneTransferResult>();

            await Assert.ThrowsAsync<DnsClientException>(async () => {
                await foreach (ZoneTransferResult rrset in client.ZoneTransferStreamAsync(
                    "example.com", maxRetries: 2, retryDelayMs: 10, cancellationToken: cts.Token)) {
                    yielded.Add(rrset);
                }
            });

            Assert.Equal(2, yielded.Count);
            Assert.Equal(1, await server.Connections);
        }

        private static AxfrServer RunAxfrServerTruncatedAsync(byte[] response, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            async Task Serve() {
#if NETFRAMEWORK
                using TcpClient client = await listener.AcceptTcpClientAsync();
#else
                using TcpClient client = await listener.AcceptTcpClientAsync(token);
#endif
                NetworkStream stream = client.GetStream();
                byte[] len = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, len, 2, token);
                if (BitConverter.IsLittleEndian) Array.Reverse(len);
                int qLen = BitConverter.ToUInt16(len, 0);
                byte[] q = new byte[qLen];
                await TestUtilities.ReadExactlyAsync(stream, q, qLen, token);
                response[0] = q[0];
                response[1] = q[1];
                byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
                await stream.WriteAsync(prefix, 0, prefix.Length, token);
                await stream.WriteAsync(response, 0, response.Length / 2, token);
                listener.Stop();
            }

            return new AxfrServer(port, Serve());
        }

        private static byte[] BuildInvalidOpcodeMessage(string zone) {
            using var ms = new System.IO.MemoryStream();
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0x8800);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);
            var zoneBytes = EncodeName(zone);
            ms.Write(zoneBytes, 0, zoneBytes.Length);
            WriteUInt16(ms, (ushort)DnsRecordType.AXFR);
            WriteUInt16(ms, 1);
            return ms.ToArray();
        }

        /// <summary>
        /// Fails when the response is truncated.
        /// </summary>
        [Fact]
        public async Task ZoneTransferAsync_FailsOnTruncatedResponse() {
            var soa = BuildSoaRdata();
            byte[] m1 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerTruncatedAsync(m1, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            await Assert.ThrowsAsync<DnsClientException>(() => client.ZoneTransferAsync("example.com"));
            await server.Task;
        }

        /// <summary>
        /// Rejects a response with an invalid opcode.
        /// </summary>
        [Fact]
        public async Task ZoneTransferAsync_InvalidOpcode_Throws() {
            byte[] m1 = BuildInvalidOpcodeMessage("example.com");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(new[] { m1 }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            await Assert.ThrowsAsync<DnsClientException>(() => client.ZoneTransferAsync("example.com"));
            await server.Task;
        }

        /// <summary>
        /// Streams zone transfer responses successfully.
        /// </summary>
        [Fact]
        public async Task ZoneTransferStreamAsync_ReturnsRecords() {
            var soa = BuildSoaRdata();
            byte[] m1 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            byte[] m2 = BuildMessage("example.com", ("www.example.com", DnsRecordType.A, new byte[] { 1, 2, 3, 4 }));
            byte[] m3 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(new[] { m1, m2, m3 }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            var results = new System.Collections.Generic.List<ZoneTransferResult>();
            await foreach (var rrset in client.ZoneTransferStreamAsync("example.com")) {
                results.Add(rrset);
            }
            await server.Task;

            Assert.Equal(3, results.Count);
            Assert.True(results[0].IsOpening);
            Assert.Equal(DnsRecordType.SOA, results[0].Records[0].Type);
            Assert.Equal(DnsRecordType.A, results[1].Records[0].Type);
            Assert.True(results[2].IsClosing);
            Assert.Equal(DnsRecordType.SOA, results[2].Records[0].Type);
        }

        /// <summary>
        /// Streaming zone transfer should fail when an error is returned.
        /// </summary>
        [Fact]
        public async Task ZoneTransferStreamAsync_FailsWithError() {
            byte[] m1 = BuildErrorMessage("example.com");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(new[] { m1 }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            await Assert.ThrowsAsync<DnsClientException>(async () => {
                await foreach (var _ in client.ZoneTransferStreamAsync("example.com")) {
                }
            });
            await server.Task;
        }
    }
}
