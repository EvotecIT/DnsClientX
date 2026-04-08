using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests recursive authoritative AXFR helpers.
    /// </summary>
    [Collection("NoParallel")]
    public class ZoneTransferRecursiveTests {
        private sealed class UdpServer {
            public UdpServer(int port, Task task) {
                Port = port;
                Task = task;
            }

            public int Port { get; }
            public Task Task { get; }
        }

        private sealed class AxfrServer {
            public AxfrServer(int port, Task task) {
                Port = port;
                Task = task;
            }

            public int Port { get; }
            public Task Task { get; }
        }

        private static byte[] EncodeName(string name) {
            name = name.TrimEnd('.');
            using var ms = new MemoryStream();
            foreach (string label in name.Split('.')) {
                byte[] bytes = Encoding.ASCII.GetBytes(label);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }

            ms.WriteByte(0);
            return ms.ToArray();
        }

        private static void WriteUInt16(Stream stream, ushort value) {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private static void WriteUInt32(Stream stream, uint value) {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private static (ushort Id, string QuestionName, DnsRecordType QuestionType) ParseQuestion(byte[] message) {
            ushort id = (ushort)((message[0] << 8) | message[1]);
            int offset = 12;
            string questionName = ReadName(message, ref offset);
            ushort qtype = (ushort)((message[offset] << 8) | message[offset + 1]);
            return (id, questionName, (DnsRecordType)qtype);
        }

        private static string ReadName(byte[] message, ref int offset) {
            var builder = new StringBuilder();
            while (offset < message.Length) {
                int length = message[offset++];
                if (length == 0) {
                    break;
                }

                if (builder.Length > 0) {
                    builder.Append('.');
                }

                builder.Append(Encoding.ASCII.GetString(message, offset, length));
                offset += length;
            }

            return builder.ToString();
        }

        private static byte[] BuildNsResponse(ushort id, string zone, params (string Authority, string? GlueAddress)[] authorities) {
            using var ms = new MemoryStream();
            WriteUInt16(ms, id);
            WriteUInt16(ms, 0x8180);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, (ushort)authorities.Length);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, (ushort)authorities.Count(item => !string.IsNullOrWhiteSpace(item.GlueAddress)));

            byte[] zoneBytes = EncodeName(zone);
            ms.Write(zoneBytes, 0, zoneBytes.Length);
            WriteUInt16(ms, (ushort)DnsRecordType.NS);
            WriteUInt16(ms, 1);

            foreach ((string authority, string? _) in authorities) {
                ms.Write(zoneBytes, 0, zoneBytes.Length);
                WriteUInt16(ms, (ushort)DnsRecordType.NS);
                WriteUInt16(ms, 1);
                WriteUInt32(ms, 60);

                byte[] authorityBytes = EncodeName(authority);
                WriteUInt16(ms, (ushort)authorityBytes.Length);
                ms.Write(authorityBytes, 0, authorityBytes.Length);
            }

            foreach ((string authority, string? glueAddress) in authorities) {
                if (string.IsNullOrWhiteSpace(glueAddress)) {
                    continue;
                }

                byte[] authorityBytes = EncodeName(authority);
                ms.Write(authorityBytes, 0, authorityBytes.Length);
                WriteUInt16(ms, (ushort)DnsRecordType.A);
                WriteUInt16(ms, 1);
                WriteUInt32(ms, 60);

                byte[] addressBytes = IPAddress.Parse(glueAddress).GetAddressBytes();
                WriteUInt16(ms, (ushort)addressBytes.Length);
                ms.Write(addressBytes, 0, addressBytes.Length);
            }

            return ms.ToArray();
        }

        private static byte[] BuildSoaRdata() {
            using var ms = new MemoryStream();
            byte[] nsBytes = EncodeName("ns1.example.com");
            ms.Write(nsBytes, 0, nsBytes.Length);
            byte[] hostBytes = EncodeName("hostmaster.example.com");
            ms.Write(hostBytes, 0, hostBytes.Length);
            WriteUInt32(ms, 1);
            WriteUInt32(ms, 3600);
            WriteUInt32(ms, 600);
            WriteUInt32(ms, 86400);
            WriteUInt32(ms, 60);
            return ms.ToArray();
        }

        private static byte[] BuildAxfrMessage(string zone, params (string Name, DnsRecordType Type, byte[] Data)[] answers) {
            using var ms = new MemoryStream();
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0x8400);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, (ushort)answers.Length);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);

            byte[] zoneBytes = EncodeName(zone);
            ms.Write(zoneBytes, 0, zoneBytes.Length);
            WriteUInt16(ms, (ushort)DnsRecordType.AXFR);
            WriteUInt16(ms, 1);

            foreach ((string name, DnsRecordType type, byte[] data) in answers) {
                byte[] nameBytes = EncodeName(name);
                ms.Write(nameBytes, 0, nameBytes.Length);
                WriteUInt16(ms, (ushort)type);
                WriteUInt16(ms, 1);
                WriteUInt32(ms, 3600);
                WriteUInt16(ms, (ushort)data.Length);
                ms.Write(data, 0, data.Length);
            }

            return ms.ToArray();
        }

        private static UdpServer RunDiscoveryServerAsync(Func<byte[], byte[]> buildResponse, CancellationToken token) {
            var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;

            async Task Serve() {
                using (udp) {
#if NET5_0_OR_GREATER
                    UdpReceiveResult result = await udp.ReceiveAsync(token);
                    byte[] response = buildResponse(result.Buffer);
                    await udp.SendAsync(response, result.RemoteEndPoint, token);
#else
                    var receiveTask = udp.ReceiveAsync();
                    var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));
                    if (completed != receiveTask) {
                        throw new OperationCanceledException(token);
                    }

                    UdpReceiveResult result = receiveTask.Result;
                    byte[] response = buildResponse(result.Buffer);
                    await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
#endif
                }
            }

            return new UdpServer(port, Serve());
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
                using NetworkStream stream = client.GetStream();
                byte[] len = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, len, 2, token);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(len);
                }

                int qLen = BitConverter.ToUInt16(len, 0);
                byte[] query = new byte[qLen];
                await TestUtilities.ReadExactlyAsync(stream, query, qLen, token);

                foreach (byte[] response in responses) {
                    byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
                    if (BitConverter.IsLittleEndian) {
                        Array.Reverse(prefix);
                    }

#if NET5_0_OR_GREATER
                    await stream.WriteAsync(prefix, token);
                    await stream.WriteAsync(response, token);
#else
                    await stream.WriteAsync(prefix, 0, prefix.Length, token);
                    await stream.WriteAsync(response, 0, response.Length, token);
#endif
                }

                listener.Stop();
            }

            return new AxfrServer(port, Serve());
        }

        /// <summary>
        /// Ensures recursive AXFR uses glue data to reach an authoritative server.
        /// </summary>
        [Fact]
        public async Task ZoneTransferRecursiveAsync_UsesGlueAndReturnsRecords() {
            byte[] soa = BuildSoaRdata();
            byte[] opening = BuildAxfrMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            byte[] record = BuildAxfrMessage("example.com", ("www.example.com", DnsRecordType.A, new byte[] { 1, 2, 3, 4 }));
            byte[] closing = BuildAxfrMessage("example.com", ("example.com", DnsRecordType.SOA, soa));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            AxfrServer axfrServer = RunAxfrServerAsync(new[] { opening, record, closing }, cts.Token);
            UdpServer discoveryServer = RunDiscoveryServerAsync(buffer => {
                (ushort id, string questionName, DnsRecordType questionType) = ParseQuestion(buffer);
                Assert.Equal("example.com", questionName);
                Assert.Equal(DnsRecordType.NS, questionType);
                return BuildNsResponse(id, questionName, ("ns1.example.com", "127.0.0.1"));
            }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP) { EndpointConfiguration = { Port = discoveryServer.Port } };
            RecursiveZoneTransferResult result = await client.ZoneTransferRecursiveAsync("example.com", port: axfrServer.Port, cancellationToken: cts.Token);

            await Task.WhenAll(axfrServer.Task, discoveryServer.Task);

            Assert.Equal("example.com", result.Zone);
            Assert.Equal("ns1.example.com", result.SelectedAuthority);
            Assert.Equal("127.0.0.1", result.SelectedServer);
            Assert.Single(result.Authorities);
            Assert.Single(result.TriedServers);
            Assert.Equal(3, result.RecordSets.Length);
            Assert.True(result.RecordSets[0].IsOpening);
            Assert.True(result.RecordSets[2].IsClosing);
        }

        /// <summary>
        /// Ensures recursive AXFR falls back to the next authoritative server when the first one fails.
        /// </summary>
        [Fact]
        public async Task ZoneTransferRecursiveAsync_FallsBackToNextAuthority() {
            byte[] soa = BuildSoaRdata();
            byte[] opening = BuildAxfrMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            byte[] record = BuildAxfrMessage("example.com", ("www.example.com", DnsRecordType.A, new byte[] { 5, 6, 7, 8 }));
            byte[] closing = BuildAxfrMessage("example.com", ("example.com", DnsRecordType.SOA, soa));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            AxfrServer axfrServer = RunAxfrServerAsync(new[] { opening, record, closing }, cts.Token);
            UdpServer discoveryServer = RunDiscoveryServerAsync(buffer => {
                (ushort id, string questionName, DnsRecordType questionType) = ParseQuestion(buffer);
                Assert.Equal("example.com", questionName);
                Assert.Equal(DnsRecordType.NS, questionType);
                return BuildNsResponse(
                    id,
                    questionName,
                    ("ns1.example.com", "127.0.0.2"),
                    ("ns2.example.com", "127.0.0.1"));
            }, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                EndpointConfiguration = { Port = discoveryServer.Port, TimeOut = 500 }
            };
            RecursiveZoneTransferResult result = await client.ZoneTransferRecursiveAsync("example.com", port: axfrServer.Port, retryDelayMs: 0, cancellationToken: cts.Token);

            await Task.WhenAll(axfrServer.Task, discoveryServer.Task);

            Assert.Equal("ns2.example.com", result.SelectedAuthority);
            Assert.Equal("127.0.0.1", result.SelectedServer);
            Assert.Equal(2, result.TriedServers.Length);
            Assert.Equal("127.0.0.2", result.TriedServers[0]);
            Assert.Equal("127.0.0.1", result.TriedServers[1]);
            Assert.Equal(3, result.RecordSets.Length);
        }
    }
}
