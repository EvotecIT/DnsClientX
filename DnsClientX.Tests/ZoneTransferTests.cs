using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
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
            Span<byte> b = stackalloc byte[2];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(b, val);
            s.Write(b);
        }

        private static void WriteUInt32(System.IO.Stream s, uint val) {
            Span<byte> b = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(b, val);
            s.Write(b);
        }

        private static byte[] BuildSoaRdata() {
            using var ms = new System.IO.MemoryStream();
            ms.Write(EncodeName("ns1.example.com"));
            ms.Write(EncodeName("hostmaster.example.com"));
            WriteUInt32(ms, 1);
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
            ms.Write(EncodeName(zone));
            WriteUInt16(ms, (ushort)DnsRecordType.AXFR);
            WriteUInt16(ms, 1);
            foreach (var a in answers) {
                ms.Write(EncodeName(a.Name));
                WriteUInt16(ms, (ushort)a.Type);
                WriteUInt16(ms, 1);
                WriteUInt32(ms, 3600);
                WriteUInt16(ms, (ushort)a.Data.Length);
                ms.Write(a.Data);
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
            ms.Write(EncodeName(zone));
            WriteUInt16(ms, (ushort)DnsRecordType.AXFR);
            WriteUInt16(ms, 1);
            return ms.ToArray();
        }

        private static int GetFreePort() {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }

        private static async Task RunAxfrServerAsync(int port, byte[][] responses, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            using TcpClient client = await listener.AcceptTcpClientAsync();
            NetworkStream stream = client.GetStream();
            byte[] len = new byte[2];
            await stream.ReadAsync(len, 0, 2, token);
            if (BitConverter.IsLittleEndian) Array.Reverse(len);
            int qLen = BitConverter.ToUInt16(len, 0);
            byte[] q = new byte[qLen];
            await stream.ReadAsync(q, 0, qLen, token);
            foreach (var r in responses) {
                byte[] prefix = BitConverter.GetBytes((ushort)r.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
                await stream.WriteAsync(prefix, 0, prefix.Length, token);
                await stream.WriteAsync(r, 0, r.Length, token);
            }
            listener.Stop();
        }

        [Fact]
        public async Task ZoneTransferAsync_ReturnsRecords() {
            int port = GetFreePort();
            var soa = BuildSoaRdata();
            byte[] m1 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            byte[] m2 = BuildMessage("example.com", ("www.example.com", DnsRecordType.A, new byte[] {1,2,3,4}));
            byte[] m3 = BuildMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(port, new[] { m1, m2, m3 }, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port };
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = port } };
            var records = await client.ZoneTransferAsync("example.com");
            await server;

            Assert.Equal(3, records.Length);
            Assert.Equal(DnsRecordType.SOA, records[0][0].Type);
            Assert.Equal(DnsRecordType.A, records[1][0].Type);
            Assert.Equal(DnsRecordType.SOA, records[2][0].Type);
        }

        [Fact]
        public async Task ZoneTransferAsync_FailsWithError() {
            int port = GetFreePort();
            byte[] m1 = BuildErrorMessage("example.com");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunAxfrServerAsync(port, new[] { m1 }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = port } };
            await Assert.ThrowsAsync<DnsClientException>(() => client.ZoneTransferAsync("example.com"));
            await server;
        }
    }
}
