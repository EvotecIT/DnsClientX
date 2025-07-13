using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class TsigUpdateTests {
        private static byte[] EncodeName(string name) {
            using var ms = new MemoryStream();
            foreach (string part in name.TrimEnd('.').Split('.')) {
                byte[] bytes = Encoding.ASCII.GetBytes(part);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }
            ms.WriteByte(0);
            return ms.ToArray();
        }

        private static void WriteUInt16(Stream stream, ushort value) {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value));
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteUInt32(Stream stream, uint value) {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)value));
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] BuildResponse(DnsResponseCode code) {
            using var ms = new MemoryStream();
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

        private static byte[] CreateAddMessage(string zone, string name, DnsRecordType type, string data, int ttl) {
            using var ms = new MemoryStream();
            Random rand = new();
            WriteUInt16(ms, (ushort)rand.Next(ushort.MinValue, ushort.MaxValue));
            WriteUInt16(ms, 0x2800);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0);
            WriteName(ms, zone);
            WriteUInt16(ms, (ushort)DnsRecordType.SOA);
            WriteUInt16(ms, 1);
            WriteName(ms, name);
            WriteUInt16(ms, (ushort)type);
            WriteUInt16(ms, 1);
            WriteUInt32(ms, (uint)ttl);
            byte[] rdata = BuildRdata(type, data);
            WriteUInt16(ms, (ushort)rdata.Length);
            ms.Write(rdata, 0, rdata.Length);
            return ms.ToArray();
        }

        private static byte[] BuildRdata(DnsRecordType type, string data) {
            return type switch {
                DnsRecordType.A => IPAddress.Parse(data).GetAddressBytes(),
                DnsRecordType.AAAA => IPAddress.Parse(data).GetAddressBytes(),
                DnsRecordType.CNAME or DnsRecordType.NS => BuildNameRdata(data),
                DnsRecordType.TXT => BuildTxtRdata(data),
                _ => Encoding.ASCII.GetBytes(data)
            };
        }

        private static byte[] BuildNameRdata(string name) {
            using var ms = new MemoryStream();
            WriteName(ms, name);
            return ms.ToArray();
        }

        private static byte[] BuildTxtRdata(string text) {
            using var ms = new MemoryStream();
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
            return ms.ToArray();
        }

        private static void WriteName(Stream stream, string name) {
            foreach (string part in name.TrimEnd('.').Split('.')) {
                byte[] bytes = Encoding.ASCII.GetBytes(part);
                stream.WriteByte((byte)bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }
            stream.WriteByte(0);
        }

        private static byte[] CreateTsigRecord(string keyName, string algorithm, byte[] key, byte[] message) {
            using HMACSHA256 hmac = new(key);
            byte[] mac = hmac.ComputeHash(message);
            using var ms = new MemoryStream();
            WriteName(ms, keyName);
            WriteUInt16(ms, (ushort)DnsRecordType.TSIG);
            WriteUInt16(ms, 255);
            WriteUInt32(ms, 0);
            using var rdata = new MemoryStream();
            WriteName(rdata, algorithm);
            WriteUInt16(rdata, (ushort)mac.Length);
            rdata.Write(mac, 0, mac.Length);
            byte[] rdataBytes = rdata.ToArray();
            WriteUInt16(ms, (ushort)rdataBytes.Length);
            ms.Write(rdataBytes, 0, rdataBytes.Length);
            return ms.ToArray();
        }

        private sealed class TsigServer {
            public int Port { get; }
            public Task Task { get; }
            public TsigServer(int port, Task task) { Port = port; Task = task; }
        }

        private static TsigServer RunServerAsync(byte[] update, byte[] response, byte[] key, CancellationToken token) {
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
                await stream.ReadAsync(len, 0, 2, token);
                if (BitConverter.IsLittleEndian) Array.Reverse(len);
                int qLen = BitConverter.ToUInt16(len, 0);
                byte[] q = new byte[qLen];
                await stream.ReadAsync(q, 0, qLen, token);
                Assert.True(qLen >= update.Length);
                byte[] receivedUpdate = new byte[update.Length];
                Array.Copy(q, 0, receivedUpdate, 0, receivedUpdate.Length);
                Assert.Equal(update, receivedUpdate);
                byte[] mac = ParseMac(q.AsSpan(update.Length));
                using HMACSHA256 hmac = new(key);
                byte[] expectedMac = hmac.ComputeHash(update);
                Assert.Equal(expectedMac, mac);
                byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
                await stream.WriteAsync(prefix, 0, prefix.Length, token);
                await stream.WriteAsync(response, 0, response.Length, token);
                listener.Stop();
            }

            return new TsigServer(port, Serve());
        }

        private static byte[] ParseMac(ReadOnlySpan<byte> tsig) {
            int pos = 0;
            SkipName(tsig, ref pos);
            pos += 2 + 2 + 4;
            ushort rdlen = ReadUInt16(tsig, ref pos);
            int start = pos;
            SkipName(tsig, ref pos);
            ushort size = ReadUInt16(tsig, ref pos);
            ReadOnlySpan<byte> mac = tsig.Slice(pos, size);
            return mac.ToArray();
        }

        private static void SkipName(ReadOnlySpan<byte> span, ref int pos) {
            while (pos < span.Length && span[pos] != 0) {
                int len = span[pos];
                pos += 1 + len;
            }
            pos++;
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> span, ref int pos) {
            ushort val = (ushort)((span[pos] << 8) | span[pos + 1]);
            pos += 2;
            return val;
        }

        [Fact]
        public async Task SignedUpdate_VerifiesMac() {
            byte[] key = Encoding.ASCII.GetBytes("secret");
            byte[] update = CreateAddMessage("example.com", "tsig.example.com", DnsRecordType.A, "1.2.3.4", 300);
            byte[] tsig = CreateTsigRecord("tsig-key", "hmac-sha256.", key, update);
            byte[] message = new byte[update.Length + tsig.Length];
            Buffer.BlockCopy(update, 0, message, 0, update.Length);
            Buffer.BlockCopy(tsig, 0, message, update.Length, tsig.Length);
            byte[] response = BuildResponse(DnsResponseCode.NoError);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = RunServerAsync(update, response, key, cts.Token);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port);
            using NetworkStream stream = client.GetStream();
            byte[] lenPrefix = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)message.Length));
            await stream.WriteAsync(lenPrefix, 0, lenPrefix.Length, cts.Token);
            await stream.WriteAsync(message, 0, message.Length, cts.Token);
            byte[] len = new byte[2];
            await stream.ReadAsync(len, 0, 2, cts.Token);
            if (BitConverter.IsLittleEndian) Array.Reverse(len);
            int respLen = BitConverter.ToUInt16(len, 0);
            byte[] resp = new byte[respLen];
            await stream.ReadAsync(resp, 0, respLen, cts.Token);
            await server.Task;
            DnsResponseCode code = (DnsResponseCode)(resp[3] & 0x0F);
            Assert.Equal(DnsResponseCode.NoError, code);
        }
    }
}

