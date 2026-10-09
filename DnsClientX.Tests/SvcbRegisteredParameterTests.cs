using System.Text.Json;

namespace DnsClientX.Tests;

/// <summary>Protects registered SVCB encodings and provider-independent service views.</summary>
public class SvcbRegisteredParameterTests {
    /// <summary>Alias parameters remain evidence without advertising usable service settings.</summary>
    [Fact]
    public void AliasViewsIgnoreParametersButKeepTheirBytes() {
        const string text = "0 alias.example. mandatory=port alpn=h2 no-default-alpn port=443 ipv4hint=192.0.2.1 ipv6hint=2001:db8::1 ech=AQID";
        var record = Assert.IsType<SvcbRecord>(new DnsAnswer { Type = DnsRecordType.HTTPS, DataRaw = text }.TypedRecord);
        Assert.Equal(7, record.Parameters.Count);
        Assert.Empty(record.MandatoryKeys);
        Assert.Empty(record.Alpn);
        Assert.False(record.NoDefaultAlpn);
        Assert.Null(record.Port);
        Assert.Empty(record.Ipv4Hints);
        Assert.Empty(record.Ipv6Hints);
        Assert.Null(record.EchConfiguration);
        Assert.Equal(new byte[] { 1, 187 }, record.Parameters[3].Value);
        Assert.Equal(record.ToString(), JsonSerializer.Deserialize<SvcbRecord>(JsonSerializer.Serialize(record))!.ToString());
    }

    /// <summary>Registered names and generic numeric presentations retain the same wire bytes.</summary>
    [Theory]
    [InlineData("tls-supported-groups=29,23", (ushort)9, "001D0017")]
    [InlineData("docpath=\"dns,query\"", (ushort)10, "03646E73057175657279")]
    [InlineData("docpath=\"\"", (ushort)10, "")]
    [InlineData("docpath=\",\"", (ushort)10, "0000")]
    [InlineData("pvd", (ushort)11, "")]
    [InlineData("oots=\"do53:100,dot:10,doq:20\"", (ushort)12, "04646F35336403646F740A03646F7114")]
    public async Task RegisteredPresentationsAgreeWithWire(string parameter, ushort key, string hex) {
        byte[] value = SvcbRecordTests.Hex(hex);
        byte[] rdata = new byte[] { 0, 1, 0, (byte)(key >> 8), (byte)key, (byte)(value.Length >> 8), (byte)value.Length }.Concat(value).ToArray();
        var wire = await DnsWire.DeserializeDnsWireFormat(null, false, SvcbRecordTests.Response(DnsRecordType.SVCB, rdata));
        var text = new DnsAnswer { Type = DnsRecordType.SVCB, DataRaw = "1 . " + parameter };
        var typed = Assert.IsType<SvcbRecord>(text.TypedRecord);
        Assert.Equal(value, typed.Parameters[key].Value);
        Assert.Equal(wire.Answers[0].Data, text.Data);
        Assert.Equal(JsonSerializer.Serialize(wire.Answers[0].TypedRecord), JsonSerializer.Serialize(typed));
        var reparsed = Assert.IsType<SvcbRecord>(new DnsAnswer { Type = DnsRecordType.SVCB, DataRaw = wire.Answers[0].Data }.TypedRecord);
        Assert.Equal(value, reparsed.Parameters[key].Value);
    }

    /// <summary>Unusual valid received values use generic notation when named syntax loses evidence.</summary>
    [Theory]
    [InlineData((ushort)10, "00")]
    [InlineData((ushort)12, "03646F74FF")]
    [InlineData((ushort)12, "05646F3A6F740A")]
    public void OpaqueRegisteredValuesRoundTripExactly(ushort key, string hex) {
        byte[] value = SvcbRecordTests.Hex(hex);
        var record = new SvcbRecord(1, ".", new[] { new SvcbParameter(key, value) });
        Assert.Contains("key" + key + "=", record.ToString());
        var reparsed = Assert.IsType<SvcbRecord>(new DnsAnswer { Type = DnsRecordType.SVCB, DataRaw = record.ToString() }.TypedRecord);
        Assert.Equal(value, reparsed.Parameters[key].Value);
    }

    /// <summary>Valid IPv6 spellings retain their address value, including embedded IPv4.</summary>
    [Theory]
    [InlineData("2001:DB8:0:0:0:0:0:1", "2001:db8::1")]
    [InlineData("::ffff:192.0.2.1", "::ffff:192.0.2.1")]
    public void StandardIpv6HintsRemainUsable(string presentation, string expected) {
        var record = Assert.IsType<SvcbRecord>(new DnsAnswer {
            Type = DnsRecordType.HTTPS, DataRaw = "1 . ipv6hint=" + presentation
        }.TypedRecord);
        Assert.Equal(System.Net.IPAddress.Parse(expected), Assert.Single(record.Ipv6Hints));
    }

    /// <summary>Invalid named grammars cannot become usable typed data.</summary>
    [Theory]
    [InlineData("port=\\052\\052\\051")]
    [InlineData("mandatory=po\\114t port=443")]
    [InlineData("ipv4hint=192.0.2.\\049")]
    [InlineData("ipv6hint=2001:db8::\\049")]
    [InlineData("ech=\\065QID")]
    [InlineData("ech=\"\\065QID\"")]
    [InlineData("tls-supported-groups=\\0509,23")]
    [InlineData("tls-supported-groups=29,29")]
    [InlineData("tls-supported-groups=")]
    [InlineData("pvd=value")]
    [InlineData("oots=dot:101")]
    [InlineData("oots=dot:10,dot:20")]
    [InlineData("ipv4hint=1.1.1.010")]
    [InlineData("ipv4hint=192.0.2")]
    [InlineData("ipv4hint=0xC0000201")]
    [InlineData("ipv6hint=::ffff:1.1.1.010")]
    [InlineData("ipv6hint=2001:db8::1%0")]
    public void InvalidNamedValuesRemainOpaque(string parameter) {
        string text = "1 . " + parameter;
        var answer = new DnsAnswer { Type = DnsRecordType.HTTPS, DataRaw = text };
        Assert.Equal(text, Assert.IsType<UnknownRecord>(answer.TypedRecord).Data);
    }

    /// <summary>Named Base64 and generic escaped octets preserve the same ECH evidence.</summary>
    [Theory]
    [InlineData("ech=AQID")]
    [InlineData("key5=\"\\001\\002\\003\"")]
    public void EchNamedAndGenericValuesRetainWireBytes(string parameter) {
        var record = Assert.IsType<SvcbRecord>(new DnsAnswer {
            Type = DnsRecordType.HTTPS, DataRaw = "1 . " + parameter
        }.TypedRecord);
        Assert.Equal(new byte[] { 1, 2, 3 }, record.Parameters[5].Value);
        Assert.Equal("1 . ech=AQID", record.ToString());
    }

    /// <summary>Zone-file parsing retains parameter quotes, spaces and both escape layers.</summary>
    [Theory]
    [InlineData(DnsRecordType.SVCB)]
    [InlineData(DnsRecordType.HTTPS)]
    public void ZoneFileRetainsQuotedParameterGrammar(DnsRecordType type) {
        const string parameters = "key667=\"hello world\" alpn=\"foo\\\\,bar,h2\" docpath=\"dns,query\"";
        var result = DnsZoneFileParser.Parse("$ORIGIN example.com.\n$TTL 60\n@ IN " + type + " 1 service " + parameters);
        Assert.True(result.Success);
        var answer = Assert.Single(result.Records);
        var parsed = Assert.IsType<SvcbRecord>(answer.TypedRecord);
        Assert.Equal("service.example.com", parsed.Target);
        Assert.Equal("1 service.example.com. " + parameters, answer.DataRaw);
        Assert.Equal(new[] { "foo,bar", "h2" }, parsed.Alpn);
        Assert.Equal(System.Text.Encoding.ASCII.GetBytes("hello world"), parsed.Parameters[667].Value);
    }
}
