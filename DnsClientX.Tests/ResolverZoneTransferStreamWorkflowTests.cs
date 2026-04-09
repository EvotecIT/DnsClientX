using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared direct AXFR stream workflows.
    /// </summary>
    [Collection("NoParallel")]
    public class ResolverZoneTransferStreamWorkflowTests {
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

                int queryLength = BitConverter.ToUInt16(len, 0);
                byte[] query = new byte[queryLength];
                await TestUtilities.ReadExactlyAsync(stream, query, queryLength, token);

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
        /// Ensures direct AXFR stream workflows execute against explicit targets.
        /// </summary>
        [Fact]
        public async Task StreamAsync_ExecutesAgainstExplicitTarget() {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            byte[] soa = BuildSoaRdata();
            AxfrServer transferServer = RunAxfrServerAsync(new[] {
                BuildAxfrMessage("example.com", ("example.com", DnsRecordType.SOA, soa)),
                BuildAxfrMessage("example.com", ("www.example.com", DnsRecordType.A, IPAddress.Parse("192.0.2.10").GetAddressBytes())),
                BuildAxfrMessage("example.com", ("example.com", DnsRecordType.SOA, soa))
            }, cts.Token);

            var results = new List<ZoneTransferResult>();
            await foreach (ZoneTransferResult recordSet in ResolverZoneTransferWorkflow.StreamAsync(
                               new ResolverExecutionTarget {
                                   DisplayName = $"tcp@127.0.0.1:{transferServer.Port}",
                                   ExplicitEndpoint = new DnsResolverEndpoint {
                                       Transport = Transport.Tcp,
                                       Host = "127.0.0.1",
                                       Port = transferServer.Port
                                   }
                               },
                               "example.com",
                               cancellationToken: cts.Token)) {
                results.Add(recordSet);
            }

            await transferServer.Task;
            Assert.Equal(3, results.Count);
            Assert.True(results[0].IsOpening);
            Assert.Equal(DnsRecordType.A, results[1].Records[0].Type);
            Assert.True(results[2].IsClosing);
        }
    }
}
