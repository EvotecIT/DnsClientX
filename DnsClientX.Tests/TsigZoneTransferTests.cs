using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Exercises authenticated transfer boundaries using an independent BCL fixture signer.</summary>
    public sealed class TsigZoneTransferTests {
        private static readonly byte[] Secret = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        private const string KeyName = "transfer-key.example.com.";

        /// <summary>Both fully signed and legacy sparse signatures authenticate the same complete AXFR.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(99)]
        public async Task AuthenticatesSignedAxfrAndLegacyIntermediaries(int unsignedCount) {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var server = new TransferServer(q => CreateFrames(q, unsignedCount, null), cts.Token);
            using var client = CreateClient(server.Port);
            ZoneTransferResult[] result = await client.ZoneTransferAsync("example.com", retryOnTransient: false,
                cancellationToken: cts.Token);
            await server.Completion;
            Assert.Equal(Math.Max(1, unsignedCount) + 2, result.Sum(r => r.Records.Length));
            Assert.True(result[0].IsOpening);
            Assert.True(result[result.Length - 1].IsClosing);
        }

        /// <summary>Unauthenticated intermediary records never reach streaming callers when the chain fails.</summary>
        [Theory]
        [InlineData("unsigned-first")]
        [InlineData("unsigned-last")]
        [InlineData("tampered")]
        [InlineData("wrong-key")]
        [InlineData("too-many")]
        public async Task RejectsBrokenChainWithoutLeakingUnsignedRecords(string failure) {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var server = new TransferServer(q => CreateFrames(q, failure == "too-many" ? 100 : 1, failure), cts.Token);
            using var client = CreateClient(server.Port);
            var observed = new List<DnsAnswer>();
            await Assert.ThrowsAsync<DnsClientException>(async () => {
                await foreach (ZoneTransferResult set in client.ZoneTransferStreamAsync("example.com",
                    retryOnTransient: false, cancellationToken: cts.Token)) observed.AddRange(set.Records);
            });
            await server.Completion;
            Assert.DoesNotContain(observed, a => a.Type == DnsRecordType.A);
        }

        /// <summary>The same chain verifier protects incremental transfers and their full-transfer fallback.</summary>
        [Fact]
        public async Task AuthenticatesIxfrFallback() {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var server = new TransferServer(q => CreateFrames(q, 1, null), cts.Token);
            using var client = CreateClient(server.Port);
            IncrementalZoneTransferResult result = await client.IncrementalZoneTransferAsync("example.com", 0, cts.Token);
            await server.Completion;
            Assert.Equal(IncrementalZoneTransferKind.FullTransfer, result.Kind);
            Assert.Equal(3, result.FullZoneRecords.Count);
        }

        /// <summary>Both signed and legacy unsigned new-SOA frames must be followed through their added records.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AuthenticatesIncrementalSequenceSplitAfterNewSoa(bool sparseSignatures) {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var server = new TransferServer(q => CreateIncrementalFrames(q, sparseSignatures), cts.Token);
            using var client = CreateClient(server.Port);

            IncrementalZoneTransferResult result = await client.IncrementalZoneTransferAsync("example.com", 1, cts.Token);
            await server.Completion;

            Assert.Equal(IncrementalZoneTransferKind.Incremental, result.Kind);
            IncrementalZoneChange change = Assert.Single(result.Changes);
            Assert.Equal(1u, change.PreviousSoa.Serial);
            Assert.Equal(2u, change.CurrentSoa.Serial);
            Assert.Equal("192.0.2.1", Assert.Single(change.DeletedRecords).Data);
            Assert.Equal("192.0.2.2", Assert.Single(change.AddedRecords).Data);
        }

        /// <summary>A signed single-SOA response also completes when the client's serial is newer than the primary's.</summary>
        [Theory]
        [InlineData(8u, 7u)]
        [InlineData(0u, uint.MaxValue)]
        public async Task AuthenticatesNoChangeFromAnOlderPrimary(uint requestedSerial, uint primarySerial) {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var server = new TransferServer(q => new[] {
                Sign(Message(U16(q, 0), Soa(primarySerial)), ReadRequestMac(q), Array.Empty<byte[]>(), first: true)
            }, cts.Token);
            using var client = CreateClient(server.Port);

            IncrementalZoneTransferResult result = await client.IncrementalZoneTransferAsync("example.com", requestedSerial, cts.Token);
            await server.Completion;

            Assert.Equal(IncrementalZoneTransferKind.NoChange, result.Kind);
            Assert.Equal(primarySerial, result.CurrentSoa.Serial);
            Assert.Empty(result.Changes);
        }

        private static ClientX CreateClient(int port) => new ClientX(new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
            Port = port, TimeOut = 5000, TsigKey = new TsigKey(KeyName, Secret)
        });

        private static byte[][] CreateFrames(byte[] query, int unsignedCount, string? failure) {
            ushort id = U16(query, 0);
            byte[] requestMac = ReadRequestMac(query);
            var frames = new List<byte[]>();
            byte[] opening = Message(id, true);
            byte[] first = Sign(opening, requestMac, Array.Empty<byte[]>(), first: true);
            frames.Add(failure == "unsigned-first" ? opening : first);
            byte[] prior = ReadRequestMac(first);
            var intermediary = new List<byte[]>();
            for (int i = 0; i < Math.Max(1, unsignedCount); i++) {
                byte[] middle = Message(id, false);
                if (unsignedCount == 0) {
                    byte[] signed = Sign(middle, prior, Array.Empty<byte[]>(), first: false);
                    frames.Add(signed); prior = ReadRequestMac(signed);
                } else {
                    intermediary.Add(middle); frames.Add(middle);
                }
            }
            byte[] closing = Message(id, true);
            byte[] final = Sign(closing, prior, intermediary.ToArray(), first: false,
                failure == "wrong-key" ? new byte[] { 9, 9, 9 } : null);
            frames.Add(failure == "unsigned-last" ? closing : final);
            if (failure == "tampered") frames[1][frames[1].Length - 1] ^= 1;
            return frames.ToArray();
        }

        private static byte[][] CreateIncrementalFrames(byte[] query, bool sparseSignatures) {
            ushort id = U16(query, 0);
            byte[] opening = Sign(Message(id, Soa(2)), ReadRequestMac(query), Array.Empty<byte[]>(), first: true);
            byte[] difference = Message(id, Soa(1), Address("old.example.com.", 1), Soa(2));
            byte[] middle = sparseSignatures ? difference
                : Sign(difference, ReadRequestMac(opening), Array.Empty<byte[]>(), first: false);
            byte[] closing = Sign(Message(id, Address("new.example.com.", 2), Soa(2)),
                ReadRequestMac(sparseSignatures ? opening : middle),
                sparseSignatures ? new[] { difference } : Array.Empty<byte[]>(), first: false);
            return new[] { opening, middle, closing };
        }

        private static byte[] Message(ushort id, bool soa) => Message(id, soa ? Soa(1) : Address("www.example.com.", 1));

        private static byte[] Message(ushort id, params (string Name, DnsRecordType Type, byte[] Data)[] records) {
            using var s = new MemoryStream();
            W16(s, id); W16(s, 0x8400); W16(s, 0); W16(s, checked((ushort)records.Length)); W16(s, 0); W16(s, 0);
            foreach (var record in records) {
                Name(s, record.Name); W16(s, (ushort)record.Type); W16(s, 1); W32(s, 60);
                W16(s, checked((ushort)record.Data.Length)); s.Write(record.Data, 0, record.Data.Length);
            }
            return s.ToArray();
        }

        private static (string Name, DnsRecordType Type, byte[] Data) Soa(uint serial) {
            using var data = new MemoryStream();
            Name(data, "ns.example.com."); Name(data, "hostmaster.example.com.");
            W32(data, serial); W32(data, 3600); W32(data, 600); W32(data, 86400); W32(data, 60);
            return ("example.com.", DnsRecordType.SOA, data.ToArray());
        }

        private static (string Name, DnsRecordType Type, byte[] Data) Address(string name, byte finalOctet) =>
            (name, DnsRecordType.A, new byte[] { 192, 0, 2, finalOctet });

        private static byte[] Sign(byte[] message, byte[] prior, byte[][] intermediaries, bool first, byte[]? secret = null) {
            ulong time = checked((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            using var input = new MemoryStream(); W16(input, checked((ushort)prior.Length)); input.Write(prior, 0, prior.Length);
            foreach (byte[] m in intermediaries) input.Write(m, 0, m.Length);
            input.Write(message, 0, message.Length);
            if (first) { Name(input, KeyName); W16(input, 255); W32(input, 0); Name(input, "hmac-sha256."); }
            W48(input, time); W16(input, 300);
            if (first) { W16(input, 0); W16(input, 0); }
            using var hmac = new HMACSHA256(secret ?? Secret); byte[] mac = hmac.ComputeHash(input.ToArray());
            using var data = new MemoryStream(); Name(data, "hmac-sha256."); W48(data, time); W16(data, 300);
            W16(data, checked((ushort)mac.Length)); data.Write(mac, 0, mac.Length); W16(data, U16(message, 0)); W16(data, 0); W16(data, 0);
            using var result = new MemoryStream(); result.Write(message, 0, message.Length);
            Name(result, KeyName); W16(result, 250); W16(result, 255); W32(result, 0);
            byte[] rdata = data.ToArray(); W16(result, checked((ushort)rdata.Length)); result.Write(rdata, 0, rdata.Length);
            byte[] output = result.ToArray(); output[11] = 1; return output;
        }

        internal static byte[] ReadRequestMac(byte[] message) {
            var r = new DnsWireReader(message); r.Skip(4);
            int questions = r.ReadUInt16(), answers = r.ReadUInt16(), authority = r.ReadUInt16(), additional = r.ReadUInt16();
            for (int i = 0; i < questions; i++) { r.ReadName(); r.Skip(4); }
            for (int i = 0; i < answers + authority + additional; i++) {
                r.ReadName(); ushort type = r.ReadUInt16(); r.Skip(6); ushort length = r.ReadUInt16();
                if (type == 250) { r.ReadName(); r.Skip(8); return r.ReadBytes(r.ReadUInt16()); }
                r.Skip(length);
            }
            throw new InvalidDataException("The transfer query was not signed.");
        }

        private static void Name(Stream s, string name) {
            foreach (string label in name.TrimEnd('.').Split('.')) {
                s.WriteByte((byte)label.Length); byte[] b = System.Text.Encoding.ASCII.GetBytes(label); s.Write(b, 0, b.Length);
            }
            s.WriteByte(0);
        }
        private static ushort U16(byte[] b, int offset) => (ushort)((b[offset] << 8) | b[offset + 1]);
        private static void W16(Stream s, ushort n) { s.WriteByte((byte)(n >> 8)); s.WriteByte((byte)n); }
        private static void W32(Stream s, uint n) { for (int shift = 24; shift >= 0; shift -= 8) s.WriteByte((byte)(n >> shift)); }
        private static void W48(Stream s, ulong n) { for (int shift = 40; shift >= 0; shift -= 8) s.WriteByte((byte)(n >> shift)); }

        private sealed class TransferServer : IDisposable {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            internal TransferServer(Func<byte[], byte[][]> responses, CancellationToken token) {
                listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port; Completion = ServeAsync(responses, token);
            }
            internal int Port { get; }
            internal Task Completion { get; }
            private async Task ServeAsync(Func<byte[], byte[][]> responses, CancellationToken token) {
                using TcpClient tcp = await listener.AcceptTcpClientAsync(); using NetworkStream stream = tcp.GetStream();
                byte[] length = new byte[2]; await TestUtilities.ReadExactlyAsync(stream, length, 2, token);
                byte[] query = new byte[U16(length, 0)]; await TestUtilities.ReadExactlyAsync(stream, query, query.Length, token);
                try {
                    foreach (byte[] frame in responses(query)) {
                        byte[] prefix = { (byte)(frame.Length >> 8), (byte)frame.Length };
                        await stream.WriteAsync(prefix, 0, 2, token); await stream.WriteAsync(frame, 0, frame.Length, token);
                    }
                } catch (IOException) { /* Expected when the client rejects a broken chain and closes TCP. */ }
            }
            public void Dispose() => listener.Stop();
        }
    }
}
