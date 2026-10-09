using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DnsClientX.Tests;

/// <summary>Protects shared SVCB/HTTPS decoding, opaque data, and RFC 9460 escaping.</summary>
public class SvcbRecordTests {
    /// <summary>Typed JSON stores canonical parameter bytes and restores every computed view.</summary>
    [Theory]
    [InlineData("1 .")]
    [InlineData("1 . alpn=h2 port=443 ipv4hint=192.0.2.1 ipv6hint=2001:db8::1 ech=AQID key65534=opaque")]
    public void TypedJsonRoundTripPreservesCanonicalData(string text) {
        var record = Assert.IsType<SvcbRecord>(new DnsAnswer { Type = DnsRecordType.HTTPS, DataRaw = text }.TypedRecord);
        string json = DnsClientXJsonSerializer.Serialize(record);
        var restored = DnsClientXJsonSerializer.Deserialize<SvcbRecord>(json)!;
        Assert.Equal(record.ToString(), restored.ToString());
        Assert.Equal(record.Ipv4Hints, restored.Ipv4Hints);
        Assert.Equal(record.Ipv6Hints, restored.Ipv6Hints);
        Assert.Equal(record.Alpn, restored.Alpn);
        Assert.Equal(record.EchConfiguration, restored.EchConfiguration);
        Assert.Equal(record.ToString(), JsonSerializer.Deserialize<SvcbRecord>(JsonSerializer.Serialize(record))!.ToString());
    }

    /// <summary>Contradictory JSON parameter keys cannot create an ambiguous record.</summary>
    [Fact]
    public void TypedJsonRejectsMismatchedDictionaryKey() {
        Assert.Throws<ArgumentException>(() => DnsClientXJsonSerializer.Deserialize<SvcbRecord>(
            "{\"Priority\":1,\"Target\":\".\",\"Parameters\":{\"4\":{\"Key\":6,\"Value\":\"AAAAAAAAAAAAAAAAAAAAAA==\"}}}"));
    }

    /// <summary>Near-limit valid lists retain all values through wire and presentation decoding.</summary>
    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)4)]
    public async Task LargeParameterListsPreserveAllValues(ushort key) {
        var value = new byte[60000];
        if (key == 1) for (int offset = 0; offset < value.Length; offset += 2) { value[offset] = 1; value[offset + 1] = (byte)'x'; }
        var record = new SvcbRecord(1, ".", new[] { new SvcbParameter(key, value) });
        var rdata = new byte[value.Length + 7];
        rdata[1] = 1;
        rdata[4] = (byte)key;
        rdata[5] = (byte)(value.Length >> 8);
        rdata[6] = (byte)(value.Length & 255);
        Buffer.BlockCopy(value, 0, rdata, 7, value.Length);
        var wire = await DnsWire.DeserializeDnsWireFormat(null, false, Response(DnsRecordType.HTTPS, rdata));
        Assert.Equal(record.ToString(), wire.Answers[0].Data);
        var parsed = Assert.IsType<SvcbRecord>(new DnsAnswer { Type = DnsRecordType.HTTPS, DataRaw = record.ToString() }.TypedRecord);
        Assert.Equal(value, parsed.Parameters[key].Value);
        Assert.Equal(key == 1 ? 30000 : 15000, key == 1 ? parsed.Alpn.Count : parsed.Ipv4Hints.Count);
    }

    /// <summary>Wire, named JSON and generic JSON presentations describe the same typed record.</summary>
    [Theory]
    [InlineData(DnsRecordType.SVCB)]
    [InlineData(DnsRecordType.HTTPS)]
    public async Task ProviderPresentationsPreserveEquivalentTypedData(DnsRecordType type) {
        byte[] rdata = Hex("0010 03466F6F 074578616D706C65 00 0000000400010003 00010006026832026833 0003000201BB 00050003010203 FFFE00030001FF");
        var wire = await DnsWire.DeserializeDnsWireFormat(null, false, Response(type, rdata));
        const string named = "016 Foo.Example. key65534=\"\\000\\001\\255\" ech=\"AQID\" port=\"0443\" alpn=\"h2,h3\" mandatory=port,alpn";
        const string expected = "16 foo.example mandatory=alpn,port alpn=\"h2,h3\" port=443 ech=AQID key65534=\"\\000\\001\\255\"";
        foreach (string raw in new[] { named, "\\# " + rdata.Length + " " + BitConverter.ToString(rdata).Replace("-", " ") }) {
            string json = JsonSerializer.Serialize(new { Status = 0, Answer = new[] { new { name = "example.com.", type = (ushort)type, TTL = 60, data = raw } } });
            using var http = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/dns-json") };
            var response = await http.DeserializeResponse();
            Assert.Equal(expected, wire.Answers[0].Data);
            Assert.Equal(expected, response.Answers[0].Data);
            Assert.Equal(raw, response.Answers[0].DataRaw);
            var typed = Assert.IsType<SvcbRecord>(response.Answers[0].TypedRecord);
            Assert.Equal((ushort)16, typed.Priority);
            Assert.Equal("foo.example", typed.Target);
            Assert.Equal(new ushort[] { 1, 3 }, typed.MandatoryKeys);
            Assert.Equal(new[] { "h2", "h3" }, typed.Alpn);
            Assert.Equal((ushort)443, typed.Port);
            Assert.Equal(new byte[] { 1, 2, 3 }, typed.EchConfiguration);
            Assert.Equal(new byte[] { 0, 1, 255 }, typed.Parameters[65534].Value);
            Assert.Equal(JsonSerializer.Serialize(wire.Answers[0].TypedRecord), JsonSerializer.Serialize(typed));
        }
    }

    /// <summary>RFC 9460 Appendix D vectors preserve octets through both escaping layers.</summary>
    [Theory]
    [InlineData("1 foo.example.com. key667=\"hello\\210qoo\"", "0001 03666F6F076578616D706C6503636F6D00 029B0009 68656C6C6FD2716F6F")]
    [InlineData("16 foo.example.org. alpn=\"f\\\\\\\\oo\\\\,bar,h2\"", "0010 03666F6F076578616D706C65036F726700 0001000C 08665C6F6F2C626172026832")]
    [InlineData("16 foo.example.org. alpn=f\\\\\\092oo\\092,bar,h2", "0010 03666F6F076578616D706C65036F726700 0001000C 08665C6F6F2C626172026832")]
    public async Task PublishedEscapingVectorsAgree(string presentation, string wireHex) {
        var wire = await DnsWire.DeserializeDnsWireFormat(null, false, Response(DnsRecordType.SVCB, Hex(wireHex)));
        var text = new DnsAnswer { Type = DnsRecordType.SVCB, DataRaw = presentation };
        Assert.IsType<SvcbRecord>(text.TypedRecord);
        Assert.Equal(wire.Answers[0].Data, text.Data);
        Assert.Equal(JsonSerializer.Serialize(wire.Answers[0].TypedRecord), JsonSerializer.Serialize(text.TypedRecord));
    }

    /// <summary>Numeric key notation carries wire bytes, even for a recognized parameter.</summary>
    [Fact]
    public void NumericKeyUsesWireEncodingAndDefensiveCopies() {
        var answer = new DnsAnswer { Type = DnsRecordType.HTTPS, DataRaw = "1 . key3=\"\\001\\187\" key1=\"\\002h2\"" };
        var typed = Assert.IsType<SvcbRecord>(answer.TypedRecord);
        Assert.Equal((ushort)443, typed.Port);
        Assert.Equal(new[] { "h2" }, typed.Alpn);
        byte[] copy = typed.Parameters[3].Value;
        copy[0] = 0;
        Assert.Equal((ushort)443, typed.Port);
        byte[] source = { 1, 187 };
        var parameter = new SvcbParameter(3, source);
        source[0] = 0;
        Assert.Equal(new byte[] { 1, 187 }, parameter.Value);
    }

    /// <summary>Malformed provider data stays available without becoming a usable typed record.</summary>
    [Theory]
    [InlineData("1 . port=443 port=8443")]
    [InlineData("1 . mandatory=port")]
    [InlineData("1 . mandatory=mandatory")]
    [InlineData("1 . mandatory=port,port port=443")]
    [InlineData("1 . alpn")]
    [InlineData("1 . alpn=\"h2,\"")]
    [InlineData("1 . no-default-alpn")]
    [InlineData("1 . no-default-alpn=abc alpn=h2")]
    [InlineData("1 . port=65536")]
    [InlineData("1 . key65535=abc")]
    [InlineData("1 . key667=\"\\999\"")]
    [InlineData("1 . ipv6hint=fe80::1%2")]
    public void MalformedPresentationRemainsOpaque(string raw) {
        var answer = new DnsAnswer { Type = DnsRecordType.SVCB, DataRaw = raw };
        Assert.Equal(raw, answer.Data);
        Assert.Equal(raw, Assert.IsType<UnknownRecord>(answer.TypedRecord).Data);
    }

    /// <summary>Wire records reject compressed targets, duplicate keys and malformed ALPN.</summary>
    [Theory]
    [InlineData("0001 C00C")]
    [InlineData("0001 00 000300020035 000300020035")]
    [InlineData("0001 00 0001000100")]
    public async Task MalformedWireIsRejected(string hex) =>
        await Assert.ThrowsAsync<DnsClientException>(() => DnsWire.DeserializeDnsWireFormat(null, false, Response(DnsRecordType.SVCB, Hex(hex))));

    /// <summary>AliasMode is explicit; address hints preserve their family and canonical form.</summary>
    [Fact]
    public void AliasAndAddressHintsAreStructured() {
        var alias = Assert.IsType<SvcbRecord>(new DnsAnswer { Type = DnsRecordType.HTTPS, DataRaw = "0 Alias.Example." }.TypedRecord);
        Assert.True(alias.IsAliasMode);
        Assert.Equal("alias.example", alias.Target);
        var service = Assert.IsType<SvcbRecord>(new DnsAnswer { Type = DnsRecordType.HTTPS,
            DataRaw = "1 . ipv6hint=\"2001:0db8::1,2001:db8::2\" ipv4hint=192.0.2.1" }.TypedRecord);
        Assert.Equal(new[] { IPAddress.Parse("192.0.2.1") }, service.Ipv4Hints);
        Assert.Equal(new[] { IPAddress.Parse("2001:db8::1"), IPAddress.Parse("2001:db8::2") }, service.Ipv6Hints);
    }

    internal static byte[] Hex(string text) {
        string compact = text.Replace(" ", string.Empty);
        return Enumerable.Range(0, compact.Length / 2).Select(index => Convert.ToByte(compact.Substring(index * 2, 2), 16)).ToArray();
    }

    internal static byte[] Response(DnsRecordType type, byte[] rdata) =>
        new byte[] { 0x12, 0x34, 0x81, 0x80, 0, 0, 0, 1, 0, 0, 0, 0, 0, (byte)((ushort)type >> 8), (byte)type, 0, 1, 0, 0, 0, 60, (byte)(rdata.Length >> 8), (byte)rdata.Length }
            .Concat(rdata).ToArray();
}
