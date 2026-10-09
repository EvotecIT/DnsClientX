using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace DnsClientX.Tests;

/// <summary>Protects evidence availability and cache isolation across all response sections.</summary>
public class DnsResponseEvidenceTests {
    /// <summary>Wire class/RDATA and full message survive cloning without exposing mutable backing arrays.</summary>
    [Fact]
    public async Task OriginalWireEvidenceIsPreservedAndIndependent() {
        byte[] message = SvcbRecordTests.Response(DnsRecordType.A, new byte[] { 192, 0, 2, 1 });
        message[15] = 0;
        message[16] = 3; // Preserve the actual RR CLASS field, even though no high-level CH query was made.
        var response = await DnsWire.DeserializeDnsWireFormat(null, false, message);
        Assert.Equal((ushort)3, response.Answers[0].Class);
        Assert.Equal(new byte[] { 192, 0, 2, 1 }, response.Answers[0].RawRdata);
        Assert.Equal(message, response.RawWireMessage);
        var clone = response.Clone();
        response.RawWireMessage![0] = 0;
        response.Answers[0].RawRdata![0] = 0;
        message[0] = 0;
        Assert.Equal((byte)0x12, clone.RawWireMessage![0]);
        Assert.Equal((byte)192, clone.Answers[0].RawRdata![0]);
        response.Answers[0] = new DnsAnswer { Type = DnsRecordType.A, DataRaw = "192.0.2.2" };
        Assert.Equal(new byte[] { 192, 0, 2, 1 }, clone.Answers[0].RawRdata);
    }

    /// <summary>Evidence allocations do not change record equality used by multicast deduplication.</summary>
    [Fact]
    public async Task RecordEqualityIgnoresEvidenceAllocation() {
        byte[] message = SvcbRecordTests.Response(DnsRecordType.A, new byte[] { 192, 0, 2, 1 });
        var first = await DnsWire.DeserializeDnsWireFormat(null, false, message);
        var second = await DnsWire.DeserializeDnsWireFormat(null, false, message);
        Assert.Equal(first.Answers[0], second.Answers[0]);
        Assert.Equal(first.Answers[0].GetHashCode(), second.Answers[0].GetHashCode());
        Assert.Single(first.Answers.Concat(second.Answers).Distinct());
        second.Answers[0].Class = 3;
        Assert.NotEqual(first.Answers[0], second.Answers[0]);
    }

    /// <summary>A compressed record retains its own source context after records are combined.</summary>
    [Fact]
    public async Task CompressedRdataRetainsItsSourceContext() {
        byte[] message = SvcbRecordTests.Response(DnsRecordType.CNAME, new byte[] { 0xC0, 0x0C });
        var response = await DnsWire.DeserializeDnsWireFormat(null, false, message);
        var record = response.Answers[0];
        Assert.Equal(new byte[] { 0xC0, 0x0C }, record.RawRdata);
        Assert.Equal(message, record.SourceWireMessage);
        record.SourceWireMessage![12] = 1;
        Assert.Equal((byte)0, record.SourceWireMessage![12]);
        Assert.Equal((byte)0, response.RawWireMessage![12]);
    }

    /// <summary>A JSON-only provider does not manufacture class or original wire bytes.</summary>
    [Fact]
    public async Task JsonReportsUnavailableWireEvidence() {
        using var http = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
            "{\"Status\":0,\"Answer\":[{\"name\":\"example.com.\",\"type\":1,\"TTL\":60,\"data\":\"192.0.2.1\"}]}") };
        var response = await http.DeserializeResponse();
        Assert.Null(response.RawWireMessage);
        Assert.Null(response.Answers[0].RawRdata);
        Assert.Null(response.Answers[0].Class);
        Assert.DoesNotContain("\"class\"", JsonSerializer.Serialize(response.Answers[0]));
    }

    /// <summary>Typed Authority/Additional collections follow TXT options and remain independent in cache clones.</summary>
    [Fact]
    public void TypedSectionsRetainParsingPolicyAndCacheIsolation() {
        var response = new DnsResponse {
            Answers = new[] { new DnsAnswer { Type = DnsRecordType.A, DataRaw = "192.0.2.1" } },
            Authorities = new[] { new DnsAnswer { Type = DnsRecordType.TXT, DataRaw = "\"v=DMARC1; p=reject\"" } },
            Additional = new[] { new DnsAnswer { Type = DnsRecordType.AAAA, DataRaw = "2001:db8::1" } }
        };
        response.PopulateTypedRecords(true);
        Assert.IsType<ARecord>(Assert.Single(response.TypedAnswers!));
        Assert.IsType<DmarcRecord>(Assert.Single(response.TypedAuthorities!));
        Assert.IsType<AAAARecord>(Assert.Single(response.TypedAdditional!));
        using var cache = new DnsResponseCache();
        cache.Set("sections", response, TimeSpan.FromMinutes(1));
        Assert.True(cache.TryGet("sections", out var clone));
        var address = Assert.IsType<AAAARecord>(clone.TypedAdditional![0]);
        address.Address.ScopeId = 7;
        Assert.Equal(0L, Assert.IsType<AAAARecord>(response.TypedAdditional![0]).Address.ScopeId);
        Assert.True(cache.TryGet("sections", out var later));
        Assert.Equal(0L, Assert.IsType<AAAARecord>(later.TypedAdditional![0]).Address.ScopeId);
        response.PopulateTypedRecords(false);
        Assert.IsType<TxtRecord>(Assert.Single(response.TypedAuthorities!));
        var projection = response.WithAnswers(Array.Empty<DnsAnswer>());
        Assert.Null(projection.TypedAnswers);
        Assert.Single(projection.TypedAuthorities!);
        Assert.Single(projection.TypedAdditional!);
    }
}
