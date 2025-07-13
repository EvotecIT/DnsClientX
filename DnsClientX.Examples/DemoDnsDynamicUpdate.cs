using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates sending TSIG signed dynamic DNS updates and verifying the result.
    /// </summary>
    internal static class DemoDnsDynamicUpdate {
        public static async Task ExampleAsync() {
            string server = "127.0.0.1";
            int port = 53;
            string zone = "example.com";
            string host = "tsig.example.com";
            byte[] key = Encoding.ASCII.GetBytes("secret");
            string keyName = "tsig-key";

            byte[] update = CreateAddMessage(zone, host, DnsRecordType.A, "1.2.3.4", 300);
            byte[] tsig = CreateTsigRecord(keyName, "hmac-sha256.", key, update);
            byte[] message = new byte[update.Length + tsig.Length];
            Buffer.BlockCopy(update, 0, message, 0, update.Length);
            Buffer.BlockCopy(tsig, 0, message, update.Length, tsig.Length);

            using TcpClient client = new();
            await client.ConnectAsync(server, port);
            using NetworkStream stream = client.GetStream();
            byte[] length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)message.Length));
            await stream.WriteAsync(length, 0, length.Length);
            await stream.WriteAsync(message, 0, message.Length);

            byte[] respLen = new byte[2];
            await stream.ReadAsync(respLen, 0, 2);
            if (BitConverter.IsLittleEndian) Array.Reverse(respLen);
            int respLength = BitConverter.ToUInt16(respLen, 0);
            byte[] resp = new byte[respLength];
            await stream.ReadAsync(resp, 0, resp.Length);
            DnsResponseCode code = (DnsResponseCode)(resp[3] & 0x0F);
            Console.WriteLine($"Update response: {code}");

            using var verifyClient = new ClientX(server, DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = port } };
            var verify = await verifyClient.Resolve(host, DnsRecordType.A);
            verify?.DisplayTable();
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

        private static void WriteUInt16(Stream stream, ushort value) {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value));
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteUInt32(Stream stream, uint value) {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)value));
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}

