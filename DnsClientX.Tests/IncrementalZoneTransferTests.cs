using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Tests RFC 1995 incremental zone transfer contracts.</summary>
    public sealed class IncrementalZoneTransferTests {
        /// <summary>Parses a complete multi-delta IXFR sequence atomically.</summary>
        [Fact]
        public async Task ParsesIncrementalChangeSequence() {
            byte[] response = BuildMessage("example.com",
                Soa(3),
                Soa(1),
                A("old.example.com", 192, 0, 2, 1),
                Soa(2),
                A("new.example.com", 192, 0, 2, 2),
                Soa(2),
                A("new.example.com", 192, 0, 2, 2),
                Soa(3),
                A("new.example.com", 192, 0, 2, 3),
                Soa(3));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            TransferServer server = RunServer(response, cts.Token);
            using var client = CreateClient(server.Port);

            IncrementalZoneTransferResult result = await client.IncrementalZoneTransferAsync(
                "example.com", 1, cts.Token);
            byte[] request = await server.Request;

            Assert.Equal(IncrementalZoneTransferKind.Incremental, result.Kind);
            Assert.Equal((uint)3, result.CurrentSoa.Serial);
            Assert.Equal(2, result.Changes.Count);
            Assert.Equal((uint)1, result.Changes[0].PreviousSoa.Serial);
            Assert.Equal((uint)2, result.Changes[0].CurrentSoa.Serial);
            Assert.Single(result.Changes[0].DeletedRecords);
            Assert.Single(result.Changes[0].AddedRecords);
            Assert.Equal((ushort)1, ReadUInt16(request, 8));
            Assert.Equal((ushort)DnsRecordType.IXFR, ReadUInt16(request, 25));
        }

        /// <summary>Recognizes the single-SOA no-change response.</summary>
        [Fact]
        public async Task ParsesNoChangeResponse() {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            TransferServer server = RunServer(BuildMessage("example.com", Soa(7)), cts.Token);
            using var client = CreateClient(server.Port);

            IncrementalZoneTransferResult result = await client.IncrementalZoneTransferAsync("example.com", 7, cts.Token);

            Assert.Equal(IncrementalZoneTransferKind.NoChange, result.Kind);
            Assert.Equal((uint)7, result.CurrentSoa.Serial);
        }

        /// <summary>Recognizes a standards-compliant AXFR-style fallback.</summary>
        [Fact]
        public async Task ParsesFullTransferFallback() {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            TransferServer server = RunServer(BuildMessage("example.com",
                Soa(4), A("www.example.com", 192, 0, 2, 4), Soa(4)), cts.Token);
            using var client = CreateClient(server.Port);

            IncrementalZoneTransferResult result = await client.IncrementalZoneTransferAsync("example.com", 1, cts.Token);

            Assert.Equal(IncrementalZoneTransferKind.FullTransfer, result.Kind);
            Assert.Equal(3, result.FullZoneRecords.Count);
        }

        /// <summary>Rejects gaps in the old-to-new SOA serial chain.</summary>
        [Fact]
        public async Task RejectsBrokenSerialChain() {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            TransferServer server = RunServer(BuildMessage("example.com", Soa(4), Soa(2), Soa(4), Soa(4)), cts.Token);
            using var client = CreateClient(server.Port);

            await Assert.ThrowsAsync<DnsClientException>(
                () => client.IncrementalZoneTransferAsync("example.com", 1, cts.Token));
        }

        private static ClientX CreateClient(int port) =>
            new("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = port } };

        private static (string Name, DnsRecordType Type, byte[] Data) Soa(uint serial) =>
            ("example.com", DnsRecordType.SOA, BuildSoaRdata(serial));

        private static (string Name, DnsRecordType Type, byte[] Data) A(string name, params byte[] address) =>
            (name, DnsRecordType.A, address);

        private static byte[] BuildSoaRdata(uint serial) {
            using var stream = new MemoryStream();
            WriteName(stream, "ns1.example.com");
            WriteName(stream, "hostmaster.example.com");
            WriteUInt32(stream, serial);
            WriteUInt32(stream, 3600);
            WriteUInt32(stream, 600);
            WriteUInt32(stream, 86400);
            WriteUInt32(stream, 60);
            return stream.ToArray();
        }

        private static byte[] BuildMessage(string zone,
            params (string Name, DnsRecordType Type, byte[] Data)[] answers) {
            using var stream = new MemoryStream();
            WriteUInt16(stream, 1);
            WriteUInt16(stream, 0x8400);
            WriteUInt16(stream, 1);
            WriteUInt16(stream, (ushort)answers.Length);
            WriteUInt16(stream, 0);
            WriteUInt16(stream, 0);
            WriteName(stream, zone);
            WriteUInt16(stream, (ushort)DnsRecordType.IXFR);
            WriteUInt16(stream, 1);
            foreach (var answer in answers) {
                WriteName(stream, answer.Name);
                WriteUInt16(stream, (ushort)answer.Type);
                WriteUInt16(stream, 1);
                WriteUInt32(stream, 3600);
                WriteUInt16(stream, (ushort)answer.Data.Length);
                stream.Write(answer.Data, 0, answer.Data.Length);
            }
            return stream.ToArray();
        }

        private static TransferServer RunServer(byte[] response, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var request = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task Serve() {
                try {
                    using TcpClient client = await listener.AcceptTcpClientAsync();
                    NetworkStream stream = client.GetStream();
                    byte[] length = new byte[2];
                    await TestUtilities.ReadExactlyAsync(stream, length, 2, token);
                    int count = ReadUInt16(length, 0);
                    var query = new byte[count];
                    await TestUtilities.ReadExactlyAsync(stream, query, count, token);
                    request.TrySetResult(query);
                    response[0] = query[0];
                    response[1] = query[1];
                    byte[] prefix = { (byte)(response.Length >> 8), (byte)response.Length };
                    await stream.WriteAsync(prefix, 0, prefix.Length, token);
                    await stream.WriteAsync(response, 0, response.Length, token);
                } finally {
                    listener.Stop();
                }
            }
            _ = Serve();
            return new TransferServer(((IPEndPoint)listener.LocalEndpoint).Port, request.Task);
        }

        private static void WriteName(Stream stream, string name) {
            foreach (string label in name.TrimEnd('.').Split('.')) {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(label);
                stream.WriteByte((byte)bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }
            stream.WriteByte(0);
        }

        private static void WriteUInt16(Stream stream, ushort value) {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static void WriteUInt32(Stream stream, uint value) {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static ushort ReadUInt16(byte[] value, int offset) =>
            (ushort)((value[offset] << 8) | value[offset + 1]);

        private sealed class TransferServer {
            internal TransferServer(int port, Task<byte[]> request) {
                Port = port;
                Request = request;
            }

            internal int Port { get; }
            internal Task<byte[]> Request { get; }
        }
    }
}
