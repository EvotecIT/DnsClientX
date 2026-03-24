using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Unit tests covering DNS UPDATE operations.
    /// </summary>
    public class DnsUpdateTests {
        private static ushort ReadUInt16(byte[] data, int offset) {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static uint ReadUInt32(byte[] data, int offset) {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static int SkipName(byte[] data, int offset) {
            while (data[offset] != 0) {
                offset += data[offset] + 1;
            }

            return offset + 1;
        }

        private static byte[] EncodeName(string name) {
            name = name.TrimEnd('.');
            using var ms = new System.IO.MemoryStream();
            foreach (var part in name.Split('.')) {
                var bytes = System.Text.Encoding.ASCII.GetBytes(part);
                ms.WriteByte((byte)bytes.Length);
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

        private static byte[] BuildResponse(DnsResponseCode code) {
            using var ms = new System.IO.MemoryStream();
            WriteUInt16(ms, 1);
            ushort flags = (ushort)(0x8000 | (ushort)code);
            WriteUInt16(ms, flags);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);
            var zone = EncodeName("example.com");
            ms.Write(zone, 0, zone.Length);
            WriteUInt16(ms, (ushort)DnsRecordType.SOA);
            WriteUInt16(ms, 1);
            return ms.ToArray();
        }

        private sealed class UpdateServer {
            public int Port { get; }
            public Task Task { get; }
            public UpdateServer(int port, Task task) { Port = port; Task = task; }
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
                NetworkStream stream = client.GetStream();
                byte[] len = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, len, 2, token);
                if (BitConverter.IsLittleEndian) Array.Reverse(len);
                int qLen = BitConverter.ToUInt16(len, 0);
                byte[] q = new byte[qLen];
                await TestUtilities.ReadExactlyAsync(stream, q, qLen, token);
                byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
                await stream.WriteAsync(prefix, 0, prefix.Length, token);
                await stream.WriteAsync(response, 0, response.Length, token);
                listener.Stop();
            }

            return new UpdateServer(port, Serve());
        }

        /// <summary>
        /// Sends an UPDATE request and expects a successful status.
        /// </summary>
        [Fact]
        public async Task UpdateRecordAsync_ReturnsSuccess() {
            byte[] resp = BuildResponse(DnsResponseCode.NoError);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunServerAsync(resp, cts.Token);
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            var res = await client.UpdateRecordAsync("example.com", "www.example.com", DnsRecordType.A, "1.2.3.4");
            await server.Task;
            Assert.Equal(DnsResponseCode.NoError, res.Status);
        }

        /// <summary>
        /// Sends an UPDATE request that should fail with a server error.
        /// </summary>
        [Fact]
        public async Task UpdateRecordAsync_FailsWithError() {
            byte[] resp = BuildResponse(DnsResponseCode.Refused);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunServerAsync(resp, cts.Token);
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = server.Port } };
            await Assert.ThrowsAsync<DnsClientException>(() => client.UpdateRecordAsync("example.com", "www.example.com", DnsRecordType.A, "1.2.3.4"));
            await server.Task;
        }

        /// <summary>
        /// Ensures invalid TTL values throw an exception.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task UpdateRecordAsync_InvalidTtl_Throws(int ttl) {
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => client.UpdateRecordAsync("example.com", "www.example.com", DnsRecordType.A, "1.2.3.4", ttl));
        }

        /// <summary>
        /// Long TXT payloads should be split into multiple DNS character-strings instead of truncating.
        /// </summary>
        [Fact]
        public void CreateAddMessage_LongTxt_IsSplitIntoValidChunks() {
            string text = new string('a', 300);

            byte[] message = DnsUpdateMessage.CreateAddMessage("example.com", "txt.example.com", DnsRecordType.TXT, text, 60);

            int offset = 12;
            offset = SkipName(message, offset);
            offset += 4;
            offset = SkipName(message, offset);

            Assert.Equal((ushort)DnsRecordType.TXT, ReadUInt16(message, offset));
            offset += 2;
            Assert.Equal((ushort)1, ReadUInt16(message, offset));
            offset += 2;
            Assert.Equal(60u, ReadUInt32(message, offset));
            offset += 4;

            int rdLength = ReadUInt16(message, offset);
            offset += 2;

            Assert.Equal(302, rdLength);
            Assert.Equal(255, message[offset]);
            Assert.Equal(45, message[offset + 256]);
        }
    }
}
