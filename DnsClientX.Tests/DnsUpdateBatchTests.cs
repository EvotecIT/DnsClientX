using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Protects atomic RFC 2136 batch wire contracts and authoritative failure behavior.</summary>
public sealed class DnsUpdateBatchTests {
    /// <summary>The complete RRset prerequisite and all mutations are sent in one message with RFC classes and TTLs.</summary>
    [Fact]
    public void BatchSerializesPrerequisitesAndOrderedMutationsInOneMessage() {
        var batch = new DnsUpdateBatch("example.test", new[] {
            DnsUpdateOperation.DeleteValue("host.example.test", DnsRecordType.A, "192.0.2.1"),
            DnsUpdateOperation.Add("host.example.test", DnsRecordType.A, "192.0.2.2", 45),
            DnsUpdateOperation.DeleteName("old.example.test")
        }, new[] {
            DnsUpdatePrerequisite.NameExists("host.example.test"),
            DnsUpdatePrerequisite.NameAbsent("old.example.test"),
            DnsUpdatePrerequisite.RrsetEquals("host.example.test", DnsRecordType.A, new[] { "192.0.2.1", "192.0.2.3" })
        });
        byte[] bytes = DnsUpdateMessage.CreateBatchMessage(batch).WireData;
        var reader = new DnsWireReader(bytes);
        reader.Skip(2); Assert.Equal(0x2800, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16()); Assert.Equal(4, reader.ReadUInt16());
        Assert.Equal(3, reader.ReadUInt16()); Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal("example.test", reader.ReadName().TrimEnd('.'));
        Assert.Equal((ushort)DnsRecordType.SOA, reader.ReadUInt16()); Assert.Equal(1, reader.ReadUInt16());
        AssertRecord(reader, DnsRecordType.ANY, 255, 0, Array.Empty<byte>());
        AssertRecord(reader, DnsRecordType.ANY, 254, 0, Array.Empty<byte>());
        AssertRecord(reader, DnsRecordType.A, 1, 0, IPAddress.Parse("192.0.2.1").GetAddressBytes());
        AssertRecord(reader, DnsRecordType.A, 1, 0, IPAddress.Parse("192.0.2.3").GetAddressBytes());
        AssertRecord(reader, DnsRecordType.A, 254, 0, IPAddress.Parse("192.0.2.1").GetAddressBytes());
        AssertRecord(reader, DnsRecordType.A, 1, 45, IPAddress.Parse("192.0.2.2").GetAddressBytes());
        AssertRecord(reader, DnsRecordType.ANY, 255, 0, Array.Empty<byte>());
        Assert.True(reader.IsAtEnd);
    }

    /// <summary>A failed authoritative prerequisite is returned once and is never split or replayed.</summary>
    [Theory]
    [InlineData(DnsResponseCode.NoError)]
    [InlineData(DnsResponseCode.NXRRSet)]
    public async Task BatchUsesOneAuthoritativeExchangeAndPreservesFailure(DnsResponseCode status) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<byte[]> server = ServeAsync(listener, status, deadline.Token);
        try {
            using var client = new ClientX(new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = port });
            var batch = new DnsUpdateBatch("example.test", new[] {
                DnsUpdateOperation.Add("one.example.test", DnsRecordType.A, "192.0.2.1"),
                DnsUpdateOperation.Add("two.example.test", DnsRecordType.A, "192.0.2.2")
            }, new[] { DnsUpdatePrerequisite.RrsetAbsent("one.example.test", DnsRecordType.A) });
            if (status == DnsResponseCode.NoError) {
                DnsResponse result = await client.UpdateRecordsAsync(batch, deadline.Token);
                Assert.Equal(status, result.Status);
                Assert.Equal(Transport.Tcp, result.UsedTransport);
            } else {
                DnsClientException error = await Assert.ThrowsAsync<DnsClientException>(() => client.UpdateRecordsAsync(batch, deadline.Token));
                Assert.Contains(status.ToString(), error.Message);
            }
            byte[] captured = await server;
            Assert.Equal(1, (captured[6] << 8) | captured[7]);
            Assert.Equal(2, (captured[8] << 8) | captured[9]);
            Assert.False(listener.Pending());
        } finally { listener.Stop(); }
    }

    /// <summary>Invalid owners and oversized messages cannot dispatch a partial update.</summary>
    [Fact]
    public async Task InvalidBatchFailsBeforeConnecting() {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try {
            using var client = new ClientX(new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                Port = ((IPEndPoint)listener.LocalEndpoint).Port
            });
            var outside = new DnsUpdateBatch("example.test", new[] { DnsUpdateOperation.DeleteName("outside.test") });
            await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateRecordsAsync(outside));
            var tooLarge = new DnsUpdateBatch("example.test", Enumerable.Range(0, 400).Select(i =>
                DnsUpdateOperation.Add("host.example.test", DnsRecordType.TXT, new string('x', 200))));
            await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateRecordsAsync(tooLarge));
            Assert.False(listener.Pending());
        } finally { listener.Stop(); }
    }

    /// <summary>Mutating caller catalogs after construction cannot change an atomic batch.</summary>
    [Fact]
    public void BatchSnapshotsCallerCatalogs() {
        string[] values = { "192.0.2.1" };
        DnsUpdateOperation[] operations = { DnsUpdateOperation.Add("host.example.test", DnsRecordType.A, "192.0.2.2") };
        var condition = DnsUpdatePrerequisite.RrsetEquals("host.example.test", DnsRecordType.A, values);
        var batch = new DnsUpdateBatch("example.test", operations, new[] { condition });
        values[0] = "bad"; operations[0] = DnsUpdateOperation.DeleteName("outside.test");
        Assert.Equal("192.0.2.1", batch.Prerequisites[0].Values[0]);
        Assert.Equal(DnsUpdateAction.Add, batch.Operations[0].Action);
        Assert.NotEmpty(DnsUpdateMessage.CreateBatchMessage(batch).WireData);
    }

    private static void AssertRecord(DnsWireReader reader, DnsRecordType type, ushort recordClass, uint ttl, byte[] data) {
        reader.ReadName(); Assert.Equal((ushort)type, reader.ReadUInt16());
        Assert.Equal(recordClass, reader.ReadUInt16()); Assert.Equal(ttl, reader.ReadUInt32());
        Assert.Equal(data.Length, reader.ReadUInt16()); Assert.Equal(data, reader.ReadBytes(data.Length));
    }

    private static async Task<byte[]> ServeAsync(TcpListener listener, DnsResponseCode status, CancellationToken token) {
        using TcpClient connection = await listener.AcceptTcpClientAsync();
        using NetworkStream stream = connection.GetStream();
        byte[] length = new byte[2]; await TestUtilities.ReadExactlyAsync(stream, length, 2, token);
        byte[] query = new byte[(length[0] << 8) | length[1]];
        await TestUtilities.ReadExactlyAsync(stream, query, query.Length, token);
        byte[] reply = TestUtilities.CreateResponseFromQuery(query, (ushort)(0xA800 | (ushort)status));
        byte[] prefix = { (byte)(reply.Length >> 8), (byte)reply.Length };
        await stream.WriteAsync(prefix, 0, prefix.Length, token);
        await stream.WriteAsync(reply, 0, reply.Length, token);
        return query;
    }
}
