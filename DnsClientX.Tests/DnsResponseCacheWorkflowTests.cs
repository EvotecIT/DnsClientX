using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Exercises cache lifetimes, evidence, and operations through real loopback DNS queries.</summary>
[Collection("NoParallel")]
public class DnsResponseCacheWorkflowTests {
    /// <summary>Aliases never bypass RFC 2308 or denial proof lifetimes for NXDOMAIN or NODATA.</summary>
    [Theory]
    [InlineData(DnsResponseCode.NXDomain, 10, 1, 10, 10)]
    [InlineData(DnsResponseCode.NoError, 10, 1, 10, 10)]
    [InlineData(DnsResponseCode.NXDomain, 1, 10, 10, 10)]
    [InlineData(DnsResponseCode.NXDomain, 10, 10, 1, 10)]
    [InlineData(DnsResponseCode.NoError, 10, 10, 10, 1)]
    public async Task NegativeAliasExpiresWithShortestDependency(DnsResponseCode status,
        int soaTtl, int soaMinimum, int aliasTtl, int denialTtl) {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(server);
        string name = Guid.NewGuid().ToString("N") + ".example";
        var first = await QueryNetwork(client, server, name, DnsRecordType.A,
            query => NegativeAnswer(query, status, soaTtl, aliasTtl, denialTtl, soaMinimum: soaMinimum));
        Assert.Equal(status, first.Status);
        Assert.Empty(first.Answers); // Caller projection hides the CNAME, not its lifetime dependency.
        Assert.Equal(DnsResponseSource.Cache, (await client.Resolve(name, retryOnTransient: false)).ResponseSource);
        await Task.Delay(1150);
        var later = await QueryNetwork(client, server, name, DnsRecordType.A,
            query => PositiveAnswer(query, DnsRecordType.A, 60));
        Assert.Equal(DnsResponseSource.Network, later.ResponseSource);
        Assert.Equal("192.0.2.1", Assert.Single(later.Answers).Data);
    }

    /// <summary>An alias-only negative response without SOA does not gain a guessed cache lifetime.</summary>
    [Theory]
    [InlineData(DnsResponseCode.NXDomain)]
    [InlineData(DnsResponseCode.NoError)]
    public async Task NegativeAliasWithoutSoaIsNotCached(DnsResponseCode status) {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(server);
        string name = Guid.NewGuid().ToString("N") + ".example";
        await QueryNetwork(client, server, name, DnsRecordType.A,
            query => NegativeAnswer(query, status, 10, 10, 10, includeSoa: false));
        var later = await QueryNetwork(client, server, name, DnsRecordType.A,
            query => PositiveAnswer(query, DnsRecordType.A, 60));
        Assert.Equal(DnsResponseSource.Network, later.ResponseSource);
    }

    /// <summary>All exposed TTL sections and metrics age while raw received bytes and OPT flags remain intact.</summary>
    [Fact]
    public async Task CachedSectionsAgeWithoutRewritingWireEvidence() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(server);
        string name = Guid.NewGuid().ToString("N") + ".example";
        var network = await QueryNetwork(client, server, name, DnsRecordType.A,
            query => PositiveAnswer(query, DnsRecordType.A, 60, includeSections: true), typedRecords: true);
        byte[] received = network.RawWireMessage!;
        await Task.Delay(1150);
        var cached = await client.Resolve(name, typedRecords: true, retryOnTransient: false);
        Assert.Equal(DnsResponseSource.Cache, cached.ResponseSource);
        Assert.InRange(Assert.Single(cached.Answers).TTL, 1, 59);
        Assert.Equal(cached.Answers[0].TTL, cached.AnswersMinimal[0].TTL);
        Assert.Equal(cached.Answers[0].TTL, cached.TtlMin);
        Assert.Equal((double)cached.Answers[0].TTL, cached.TtlAvg);
        Assert.InRange(Assert.Single(cached.Authorities).TTL, 1, 59);
        Assert.InRange(Assert.Single(cached.Additional, answer => answer.Type == DnsRecordType.A).TTL, 1, 59);
        Assert.Equal(Assert.Single(network.Additional, answer => answer.Type == DnsRecordType.OPT).TTL,
            Assert.Single(cached.Additional, answer => answer.Type == DnsRecordType.OPT).TTL);
        Assert.Equal(0x8000u, Assert.Single(cached.WireAdditional, record => record.Type == DnsRecordType.OPT).RawTtl);
        Assert.IsType<ARecord>(Assert.Single(cached.TypedAnswers!));
        Assert.Equal(cached.Answers[0].TTL, Assert.Single(cached.GetTypedAnswers<ARecord>()).TTL);
        Assert.Equal(cached.Authorities[0].TTL, Assert.Single(cached.GetTypedAuthorities<SoaRecord>()).TTL);
        Assert.Equal(Assert.Single(cached.Additional, answer => answer.Type == DnsRecordType.A).TTL,
            Assert.Single(cached.GetTypedAdditional<ARecord>()).TTL);
        Assert.Equal(received, cached.RawWireMessage);
        Assert.Equal(received, cached.Answers[0].SourceWireMessage);
        Assert.Equal(60, network.Answers[0].TTL);
        Assert.Equal(60, Assert.Single(cached.WireAnswers).Ttl);
    }

    /// <summary>Public invalidation selects query identity across policies; capacity and counts cover shared clients.</summary>
    [Fact]
    public async Task ProcessCacheOperationsRespectNameTypeAndPolicyBoundaries() {
        int originalCapacity = ClientX.ResponseCacheCapacity;
        ClientX.ClearResponseCache();
        var baseline = ClientX.GetResponseCacheStatistics();
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var first = CreateClient(server);
        using var second = CreateClient(server);
        first.MaxCacheTtl = TimeSpan.FromMinutes(1);
        second.MaxCacheTtl = TimeSpan.FromMinutes(2);
        const string name = "xn--bcher-kva.example";
        try {
            await QueryNetwork(first, server, name, DnsRecordType.A, query => PositiveAnswer(query, DnsRecordType.A, 60));
            await QueryNetwork(second, server, name, DnsRecordType.A, query => PositiveAnswer(query, DnsRecordType.A, 60));
            await QueryNetwork(first, server, name, DnsRecordType.AAAA, query => PositiveAnswer(query, DnsRecordType.AAAA, 60));
            await QueryNetwork(first, server, "unrelated.example", DnsRecordType.A, query => PositiveAnswer(query, DnsRecordType.A, 60));
            Assert.Equal(DnsResponseSource.Cache, (await first.Resolve(name, retryOnTransient: false)).ResponseSource);
            Assert.Equal(4, ClientX.GetResponseCacheStatistics().EntryCount);
            Assert.Equal(2, ClientX.InvalidateResponseCache("BÜCHER.EXAMPLE.", DnsRecordType.A));
            Assert.Equal(DnsResponseSource.Cache, (await first.Resolve(name, DnsRecordType.AAAA, retryOnTransient: false)).ResponseSource);
            Assert.Equal(1, ClientX.InvalidateResponseCache(name));
            Assert.Equal(1, ClientX.GetResponseCacheStatistics().EntryCount);
            await QueryNetwork(first, server, "another.example", DnsRecordType.A, query => PositiveAnswer(query, DnsRecordType.A, 60));
            ClientX.ResponseCacheCapacity = 1;
            var stats = ClientX.GetResponseCacheStatistics();
            Assert.Equal(1, stats.Capacity);
            Assert.Equal(1, stats.EntryCount);
            Assert.True(stats.Hits >= baseline.Hits + 2);
            Assert.True(stats.Misses > baseline.Misses);
            Assert.Equal(baseline.Evictions + 1, stats.Evictions);
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientX.ResponseCacheCapacity = 0);
            Assert.Equal(1, ClientX.ClearResponseCache());
            Assert.Equal(0, ClientX.GetResponseCacheStatistics().EntryCount);
        } finally {
            ClientX.ClearResponseCache();
            ClientX.ResponseCacheCapacity = originalCapacity;
        }
    }

    private static ClientX CreateClient(UdpClient server) {
        var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: 3000, enableCache: true);
        client.EndpointConfiguration.Port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        return client;
    }

    private static async Task<DnsResponse> QueryNetwork(ClientX client, UdpClient server, string name,
        DnsRecordType type, Func<byte[], byte[]> answer, bool typedRecords = false) {
        var operation = client.Resolve(name, type, typedRecords: typedRecords, retryOnTransient: false);
        var receiving = server.ReceiveAsync();
        Assert.Same(receiving, await Task.WhenAny(receiving, Task.Delay(4000)));
        var packet = await receiving;
        byte[] bytes = answer(packet.Buffer);
        await server.SendAsync(bytes, bytes.Length, packet.RemoteEndPoint);
        return await operation;
    }

    private static byte[] NegativeAnswer(byte[] query, DnsResponseCode status, int soaTtl,
        int aliasTtl, int denialTtl, bool includeSoa = true, int? soaMinimum = null) {
        byte[] header = TestUtilities.CreateResponseFromQuery(query, (ushort)(0x8180 | (int)status));
        header[7] = 1;
        header[9] = (byte)(includeSoa ? 2 : 1);
        using var response = new MemoryStream();
        response.Write(header, 0, header.Length);
        WriteRecord(response, new byte[] { 0xc0, 0x0c }, DnsRecordType.CNAME, aliasTtl, DnsWireNameCodec.ToCanonicalWire("target.example"));
        if (includeSoa) WriteRecord(response, DnsWireNameCodec.ToCanonicalWire("example"), DnsRecordType.SOA, soaTtl, SoaData(soaMinimum ?? soaTtl));
        WriteRecord(response, DnsWireNameCodec.ToCanonicalWire("target.example"), DnsRecordType.NSEC, denialTtl,
            DnsWireNameCodec.ToCanonicalWire("z.example").Concat(new byte[] { 0, 1, 0x20 }).ToArray());
        return response.ToArray();
    }

    private static byte[] PositiveAnswer(byte[] query, DnsRecordType type, int ttl, bool includeSections = false) {
        byte[] header = TestUtilities.CreateResponseFromQuery(query);
        header[7] = 1;
        header[9] = (byte)(includeSections ? 1 : 0);
        header[11] = (byte)(includeSections ? 2 : 0);
        using var response = new MemoryStream();
        response.Write(header, 0, header.Length);
        byte[] address = type == DnsRecordType.AAAA ? IPAddress.Parse("2001:db8::1").GetAddressBytes() : new byte[] { 192, 0, 2, 1 };
        WriteRecord(response, new byte[] { 0xc0, 0x0c }, type, ttl, address);
        if (includeSections) {
            WriteRecord(response, DnsWireNameCodec.ToCanonicalWire("example"), DnsRecordType.SOA, ttl, SoaData(ttl));
            WriteRecord(response, DnsWireNameCodec.ToCanonicalWire("ns.example"), DnsRecordType.A, ttl, new byte[] { 192, 0, 2, 53 });
            WriteRecord(response, new byte[] { 0 }, DnsRecordType.OPT, 0x8000, Array.Empty<byte>(), recordClass: 1232);
        }
        return response.ToArray();
    }

    private static byte[] SoaData(int minimum) {
        using var stream = new MemoryStream();
        byte[] names = DnsWireNameCodec.ToCanonicalWire("ns.example").Concat(DnsWireNameCodec.ToCanonicalWire("hostmaster.example")).ToArray();
        stream.Write(names, 0, names.Length);
        foreach (int value in new[] { 1, 3600, 600, 86400, minimum }) Write32(stream, value);
        return stream.ToArray();
    }

    private static void WriteRecord(Stream stream, byte[] name, DnsRecordType type, int ttl, byte[] rdata, int recordClass = 1) {
        stream.Write(name, 0, name.Length);
        Write16(stream, (ushort)type); Write16(stream, recordClass); Write32(stream, ttl); Write16(stream, rdata.Length);
        stream.Write(rdata, 0, rdata.Length);
    }
    private static void Write16(Stream stream, int value) { stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }
    private static void Write32(Stream stream, int value) {
        stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16)); stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value);
    }
}
