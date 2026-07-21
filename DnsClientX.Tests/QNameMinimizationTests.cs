using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Exercises RFC 9156 behavior through the real iterative UDP path.</summary>
    [Collection("NoParallel")]
    public class QNameMinimizationTests {
        /// <summary>The resolver reveals one label at a time and sends the final type only to the authoritative zone.</summary>
        [Fact]
        public async Task ResolveFromRoot_MinimizesDelegationQueries() {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
            var observed = new List<(string Name, DnsRecordType Type)>();
            Task responder = Task.Run(async () => {
                for (int index = 0; index < 4; index++) {
                    UdpReceiveResult received = await ReceiveAsync(server, timeout.Token);
                    (string name, DnsRecordType type) = ReadQuestion(received.Buffer);
                    observed.Add((name, type));
                    byte[] response = index switch {
                        0 => Referral(received.Buffer, "com", "ns.com"),
                        1 => Referral(received.Buffer, "example.com", "ns.example.com"),
                        2 => NoData(received.Buffer, "example.com"),
                        _ => Address(received.Buffer, "www.example.com", new byte[] { 192, 0, 2, 80 })
                    };
                    await SendAsync(server, response, received.RemoteEndPoint, timeout.Token);
                }
            }, timeout.Token);

            using var client = new ClientX();
            DnsResponse response = await client.ResolveFromRoot(
                "www.example.com", DnsRecordType.A, new[] { "127.0.0.1" },
                maxHops: 10, port: port, cancellationToken: timeout.Token);
            await responder;

            Assert.Equal(new[] {
                ("com", DnsRecordType.NS),
                ("example.com", DnsRecordType.NS),
                ("www.example.com", DnsRecordType.NS),
                ("www.example.com", DnsRecordType.A)
            }, observed);
            Assert.Equal(3, response.QNameMinimizedQueryCount);
            Assert.Equal(0, response.QNameMinimizationFallbackCount);
            Assert.Equal("192.0.2.80", Assert.Single(response.Answers).DataRaw);
        }

        /// <summary>A glueless referral retains every name-server address and both address families before failover.</summary>
        [Fact]
        public async Task ResolveFromRoot_RetainsAllGluelessReferralAddresses() {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
            var observed = new List<(string Name, DnsRecordType Type)>();
            Task responder = Task.Run(async () => {
                for (int index = 0; index < 6; index++) {
                    UdpReceiveResult received = await ReceiveAsync(server, timeout.Token);
                    (string name, DnsRecordType type) = ReadQuestion(received.Buffer);
                    observed.Add((name, type));
                    byte[] response = index switch {
                        0 => ReferralWithoutGlue(received.Buffer, "example.com",
                            "ns1.example.com", "ns2.example.com"),
                        1 => Address(received.Buffer, "ns1.example.com", new byte[] { 192, 0, 2, 1 }),
                        2 => NoData(received.Buffer, "example.com"),
                        3 => Address(received.Buffer, "ns2.example.com", new byte[] { 127, 0, 0, 1 }),
                        4 => NoData(received.Buffer, "example.com"),
                        _ => Address(received.Buffer, "www.example.com", new byte[] { 192, 0, 2, 80 })
                    };
                    await SendAsync(server, response, received.RemoteEndPoint, timeout.Token);
                }
            }, timeout.Token);

            using var client = new ClientX();
            client.EndpointConfiguration.EnableQNameMinimization = false;
            client.EndpointConfiguration.TimeOut = 100;
            DnsResponse response = await client.ResolveFromRoot(
                "www.example.com", DnsRecordType.A, new[] { "127.0.0.1" },
                maxHops: 10, port: port, cancellationToken: timeout.Token);
            await responder;

            Assert.Equal(new[] {
                ("www.example.com", DnsRecordType.A),
                ("ns1.example.com", DnsRecordType.A),
                ("ns1.example.com", DnsRecordType.AAAA),
                ("ns2.example.com", DnsRecordType.A),
                ("ns2.example.com", DnsRecordType.AAAA),
                ("www.example.com", DnsRecordType.A)
            }, observed);
            Assert.Equal("192.0.2.80", Assert.Single(response.Answers).DataRaw);
        }

        private static (string Name, DnsRecordType Type) ReadQuestion(byte[] message) {
            int offset = 12;
            string name = ReadName(message, ref offset);
            DnsRecordType type = (DnsRecordType)((message[offset] << 8) | message[offset + 1]);
            return (name, type);
        }

        private static byte[] Referral(byte[] query, string zone, string nameServer) {
            using var output = HeaderAndQuestion(query, flags: 0x8000, answers: 0, authorities: 1, additional: 1);
            Resource(output, zone, DnsRecordType.NS, Name(nameServer));
            Resource(output, nameServer, DnsRecordType.A, new byte[] { 127, 0, 0, 1 });
            return output.ToArray();
        }

        private static byte[] ReferralWithoutGlue(byte[] query, string zone, params string[] nameServers) {
            using var output = HeaderAndQuestion(query, flags: 0x8000, answers: 0,
                authorities: checked((ushort)nameServers.Length), additional: 0);
            foreach (string nameServer in nameServers) {
                Resource(output, zone, DnsRecordType.NS, Name(nameServer));
            }
            return output.ToArray();
        }

        private static byte[] NoData(byte[] query, string zone) {
            using var output = HeaderAndQuestion(query, flags: 0x8400, answers: 0, authorities: 1, additional: 0);
            using var soa = new MemoryStream();
            Write(soa, Name("ns.example.com"));
            Write(soa, Name("hostmaster.example.com"));
            UInt32(soa, 1);
            UInt32(soa, 3600);
            UInt32(soa, 600);
            UInt32(soa, 86400);
            UInt32(soa, 60);
            Resource(output, zone, DnsRecordType.SOA, soa.ToArray());
            return output.ToArray();
        }

        private static byte[] Address(byte[] query, string owner, byte[] address) {
            using var output = HeaderAndQuestion(query, flags: 0x8400, answers: 1, authorities: 0, additional: 0);
            Resource(output, owner, DnsRecordType.A, address);
            return output.ToArray();
        }

        private static MemoryStream HeaderAndQuestion(byte[] query, ushort flags, ushort answers,
            ushort authorities, ushort additional) {
            int offset = 12;
            ReadName(query, ref offset);
            offset += 4;
            var output = new MemoryStream();
            output.WriteByte(query[0]);
            output.WriteByte(query[1]);
            UInt16(output, flags);
            UInt16(output, 1);
            UInt16(output, answers);
            UInt16(output, authorities);
            UInt16(output, additional);
            output.Write(query, 12, offset - 12);
            return output;
        }

        private static void Resource(Stream output, string owner, DnsRecordType type, byte[] rdata) {
            Write(output, Name(owner));
            UInt16(output, (ushort)type);
            UInt16(output, 1);
            UInt32(output, 300);
            UInt16(output, checked((ushort)rdata.Length));
            Write(output, rdata);
        }

        private static string ReadName(byte[] message, ref int offset) {
            var labels = new List<string>();
            while (message[offset] != 0) {
                int length = message[offset++];
                labels.Add(Encoding.ASCII.GetString(message, offset, length));
                offset += length;
            }
            offset++;
            return string.Join(".", labels);
        }

        private static byte[] Name(string value) => DnsWireNameCodec.ToCanonicalWire(value);

        private static void UInt16(Stream output, ushort value) {
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        private static void UInt32(Stream output, uint value) {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        private static void Write(Stream output, byte[] value) => output.Write(value, 0, value.Length);

        private static async Task<UdpReceiveResult> ReceiveAsync(UdpClient server,
            CancellationToken cancellationToken) {
#if NET8_0_OR_GREATER
            return await server.ReceiveAsync(cancellationToken);
#else
            Task<UdpReceiveResult> receive = server.ReceiveAsync();
            Task completed = await Task.WhenAny(receive, Task.Delay(Timeout.Infinite, cancellationToken));
            if (completed != receive) throw new OperationCanceledException(cancellationToken);
            return await receive;
#endif
        }

        private static async Task SendAsync(UdpClient server, byte[] response, IPEndPoint endpoint,
            CancellationToken cancellationToken) {
#if NET8_0_OR_GREATER
            await server.SendAsync(response, endpoint, cancellationToken);
#else
            cancellationToken.ThrowIfCancellationRequested();
            await server.SendAsync(response, response.Length, endpoint);
#endif
        }
    }
}
