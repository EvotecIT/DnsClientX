using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DnsClientX.Tests;

/// <summary>Protects provider-independent record data and typed fields without changing raw evidence.</summary>
public class RecordDataConsistencyTests {
    /// <summary>Whitespace inside encoded payloads must not truncate keys or digests.</summary>
    [Theory]
    [InlineData(DnsRecordType.DNSKEY, "256\t3 8 AQID\r\nBA==", "256 3 RSASHA256 AQIDBA==")]
    [InlineData(DnsRecordType.DS, "20326\t8 2 aBcD\r\nef01", "20326 RSASHA256 2 ABCDEF01")]
    [InlineData(DnsRecordType.TLSA, "3\t1 1 deAd\r\nbeef", "3 1 1 DEADBEEF")]
    public void EncodedPayloadsAreComplete(DnsRecordType type, string raw, string expected) {
        var answer = new DnsAnswer { Type = type, DataRaw = raw };
        Assert.Equal(expected, answer.Data);
        object typed = answer.TypedRecord!;
        string payload = typed switch {
            DnsKeyRecord key => key.PublicKey,
            DsRecord ds => ds.Digest,
            TlsaRecord tlsa => tlsa.AssociationData,
            _ => throw new InvalidOperationException("Encoded record was not parsed.")
        };
        Assert.Equal(expected.Split(' ').Last(), payload);
        Assert.Equal(raw, answer.DataRaw);
    }

    /// <summary>Normalizing a type bitmap cannot replace text in the next name or mutate the answer.</summary>
    [Fact]
    public void NsecKeepsRawEvidenceAndUnknownTypes() {
        const string raw = "TYPE1.Example.\tTYPE1 TYPE16 TYPE65400";
        var answer = new DnsAnswer { Type = DnsRecordType.NSEC, DataRaw = raw };
        Assert.Equal("type1.example A TXT TYPE65400", answer.Data);
        Assert.Equal(raw, answer.DataRaw);
        Assert.Equal(answer.Data, Assert.IsType<UnknownRecord>(answer.TypedRecord).Data);
        Assert.Equal(raw, Assert.Single(answer.DataStrings));
    }

    /// <summary>Standard optional LOC fields use the same defaults as binary records.</summary>
    [Theory]
    [InlineData("52 N 21 E 100m")]
    [InlineData("52 0 N 21 0 E 100m 1m")]
    [InlineData("52 0 0 N 21 0 0 E 100m 1m 10000m")]
    [InlineData("52\t0 0 N 21 0 0 E 100m 1m 10000m 10m")]
    public void LocDefaultsAreParsed(string raw) {
        var answer = new DnsAnswer { Type = DnsRecordType.LOC, DataRaw = raw };
        var loc = Assert.IsType<LocRecord>(answer.TypedRecord);
        Assert.Equal(52, loc.Latitude);
        Assert.Equal(21, loc.Longitude);
        Assert.Equal(100, loc.AltitudeMeters);
        Assert.Equal(1, loc.SizeMeters);
        Assert.Equal(10000, loc.HorizontalPrecisionMeters);
        Assert.Equal(10, loc.VerticalPrecisionMeters);
        Assert.Equal(raw, answer.DataRaw);
    }

    /// <summary>Invalid binary LOC data stays available instead of throwing from a property getter.</summary>
    [Fact]
    public void MalformedLocBase64IsPreserved() {
        const string raw = "AAAA";
        var answer = new DnsAnswer { Type = DnsRecordType.LOC, DataRaw = raw };
        Assert.Equal(raw, answer.Data);
        Assert.Equal(raw, Assert.IsType<UnknownRecord>(answer.TypedRecord).Data);
    }

    /// <summary>Unrecognized hexadecimal data cannot become a guessed typed record or lose its marker.</summary>
    [Theory]
    [InlineData(DnsRecordType.CNAME)]
    [InlineData(DnsRecordType.TXT)]
    [InlineData(DnsRecordType.LOC)]
    [InlineData(DnsRecordType.DNSKEY)]
    [InlineData((DnsRecordType)65400)]
    public void MalformedGenericDataRemainsOpaque(DnsRecordType type) {
        const string raw = "\\# 3 AbCd";
        var answer = new DnsAnswer { Type = type, DataRaw = raw };
        Assert.Equal(raw, answer.Data);
        Assert.Equal(raw, Assert.IsType<UnknownRecord>(answer.TypedRecord).Data);
        Assert.Equal(raw, answer.DataRaw);
        if (type == DnsRecordType.TXT) Assert.Equal(raw, Assert.Single(answer.DataStringsEscaped));
    }

    /// <summary>Root punctuation and decimal octet escapes describe the same DNS name, not different targets.</summary>
    [Theory]
    [InlineData(DnsRecordType.NS)]
    [InlineData(DnsRecordType.CNAME)]
    [InlineData(DnsRecordType.DNAME)]
    [InlineData(DnsRecordType.PTR)]
    public void NamePresentationsAgree(DnsRecordType type) {
        foreach (string raw in new[] { @"Target\.Room.Example.", @"target\046room.example", @"\084arget\046Room.Example." }) {
            var answer = new DnsAnswer { Type = type, DataRaw = raw };
            Assert.Equal(@"target\.room.example", answer.Data);
            Assert.Equal(raw, answer.DataRaw);
        }
        Assert.Equal(".", new DnsAnswer { Type = type, DataRaw = "." }.Data);
    }

    /// <summary>Escaped spaces stay within the next-name field instead of entering the type bitmap.</summary>
    [Fact]
    public void NsecEscapedSpacePreservesTheNextName() {
        const string raw = @"Next\ Name.Example. TYPE1 TYPE16";
        var answer = new DnsAnswer { Type = DnsRecordType.NSEC, DataRaw = raw };
        Assert.Equal(@"next\032name.example A TXT", answer.Data);
        Assert.Equal(raw, answer.DataRaw);
    }

    /// <summary>Quotes in a name label are octets, not quoted-field delimiters.</summary>
    [Theory]
    [InlineData(DnsRecordType.CNAME)]
    [InlineData(DnsRecordType.NS)]
    [InlineData(DnsRecordType.DNAME)]
    [InlineData(DnsRecordType.PTR)]
    public async Task LiteralNameQuotesSurviveWireAndGenericData(DnsRecordType type) {
        byte[] rdata = Name("A\"B\"C", "Example");
        DnsResponse wire = await DnsWire.DeserializeDnsWireFormat(null, false, WireResponse(type, rdata));
        const string expected = @"a\""b\""c.example";
        Assert.Equal(expected, wire.Answers[0].Data);
        foreach (string raw in new[] { @"A\""B\""C.Example.", "\\# 15 054122422243074578616D706C6500" })
            Assert.Equal(expected, new DnsAnswer { Type = type, DataRaw = raw }.Data);
    }

    /// <summary>Generic HINFO must consume exactly two strings rather than discard trailing bytes.</summary>
    [Fact]
    public async Task HinfoGenericTrailingDataIsPreserved() {
        const string raw = "\\# 6 0141014201FF";
        var answer = new DnsAnswer { Type = DnsRecordType.HINFO, DataRaw = raw };
        Assert.Equal(raw, answer.Data);
        Assert.Equal(raw, Assert.IsType<UnknownRecord>(answer.TypedRecord).Data);
        await Assert.ThrowsAsync<DnsClientException>(() => DnsWire.DeserializeDnsWireFormat(null, false,
            WireResponse(DnsRecordType.HINFO, new byte[] { 1, 65, 1, 66, 1, 255 })));
    }

    /// <summary>Real JSON and wire parsers expose the same data for textual and generic RDATA.</summary>
    [Theory]
    [MemberData(nameof(ProviderRecords))]
    public async Task JsonWireAndGenericRdataAgree(DnsRecordType type, byte[] rdata, string presentation) {
        DnsResponse wire = await DnsWire.DeserializeDnsWireFormat(null, false, WireResponse(type, rdata));
        string expected = wire.Answers[0].Data;
        foreach (string raw in new[] { presentation, "\\# " + rdata.Length + " " + BitConverter.ToString(rdata).Replace("-", " ") }) {
            string json = "{\"Status\":0,\"Answer\":[{\"name\":\"example.com.\",\"type\":" + (ushort)type
                + ",\"TTL\":60,\"data\":" + JsonSerializer.Serialize(raw) + "}]}";
            using var http = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/dns-json") };
            DnsResponse response = await http.DeserializeResponse();
            Assert.Equal(expected, response.Answers[0].Data);
            Assert.Equal(raw, response.Answers[0].DataRaw);
            if (type is DnsRecordType.TXT or DnsRecordType.SPF)
                Assert.Equal(wire.Answers[0].DataStringsEscaped, response.Answers[0].DataStringsEscaped);
            Assert.Equal(TypedValue(wire.Answers[0]), TypedValue(response.Answers[0]));
            response.TypedAnswers = new[] { response.Answers[0].TypedRecord! };
            using var cache = new DnsResponseCache();
            cache.Set("record", response, TimeSpan.FromMinutes(1));
            response.Answers[0].DataRaw = "changed";
            Assert.True(cache.TryGet("record", out var cached));
            Assert.Equal(expected, cached.Answers[0].Data);
            Assert.Equal(raw, cached.Answers[0].DataRaw);
        }
    }

    /// <summary>Provider fixtures cover name, scalar, encoded, and quoted fields.</summary>
    public static IEnumerable<object[]> ProviderRecords() {
        byte[] name = Name("Target", "Example");
        foreach (DnsRecordType type in new[] { DnsRecordType.NS, DnsRecordType.CNAME, DnsRecordType.DNAME, DnsRecordType.PTR,
            DnsRecordType.MB, DnsRecordType.MD, DnsRecordType.MF, DnsRecordType.MG, DnsRecordType.MR })
            yield return new object[] { type, name, "Target.Example." };
        foreach (DnsRecordType type in new[] { DnsRecordType.MX, DnsRecordType.AFSDB, DnsRecordType.RT, DnsRecordType.KX })
            yield return new object[] { type, new byte[] { 0, 10 }.Concat(name).ToArray(), "10\tTarget.Example." };
        foreach (DnsRecordType type in new[] { DnsRecordType.MINFO, DnsRecordType.RP })
            yield return new object[] { type, name.Concat(name).ToArray(), "Target.Example.\tTarget.Example." };
        yield return new object[] { DnsRecordType.A, new byte[] { 192, 0, 2, 1 }, "192.0.2.1" };
        yield return new object[] { DnsRecordType.AAAA, IPAddress.Parse("2001:db8::1").GetAddressBytes(), "2001:0DB8:0:0:0:0:0:1" };
        yield return new object[] { DnsRecordType.SRV, new byte[] { 0, 1, 0, 2, 0, 80 }.Concat(name).ToArray(), "1\t2 80 Target.Example." };
        yield return new object[] { DnsRecordType.SOA, name.Concat(name).Concat(new byte[] { 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 4, 0, 0, 0, 5 }).ToArray(), "Target.Example.\tTarget.Example. 1 2 3 4 5" };
        foreach (DnsRecordType type in new[] { DnsRecordType.DNSKEY, DnsRecordType.CDNSKEY })
            yield return new object[] { type, new byte[] { 1, 0, 3, 8, 1, 2, 3, 4 }, "256 3 8 AQID BA==" };
        foreach (DnsRecordType type in new[] { DnsRecordType.DS, DnsRecordType.CDS, DnsRecordType.DLV, DnsRecordType.TA })
            yield return new object[] { type, new byte[] { 79, 102, 8, 2, 171, 205, 239, 1 }, "20326 8 2 abcd ef01" };
        foreach (DnsRecordType type in new[] { DnsRecordType.TLSA, DnsRecordType.SMIMEA })
            yield return new object[] { type, new byte[] { 3, 1, 1, 222, 173, 190, 239 }, "3 1 1 dead beef" };
        yield return new object[] { DnsRecordType.CAA, new byte[] { 0, 5, 105, 115, 115, 117, 101 }.Concat(Encoding.ASCII.GetBytes("CA.Example; Path=AbCd")).ToArray(), "0\tissue \"CA.Example; Path=AbCd\"" };
        yield return new object[] { DnsRecordType.NAPTR, new byte[] { 0, 1, 0, 2, 1, 117, 3, 115, 105, 112, 0 }.Concat(name).ToArray(), "1 2 \"u\" \"sip\" \"\" Target.Example." };
        yield return new object[] { DnsRecordType.NSEC, name.Concat(new byte[] { 0, 3, 64, 0, 128 }).ToArray(), "Target.Example. TYPE1 TYPE16" };
        byte[] quotedName = Name("A\"B\"C", "Example");
        yield return new object[] { DnsRecordType.MX, new byte[] { 0, 10 }.Concat(quotedName).ToArray(), @"10 A\""B\""C.Example." };
        yield return new object[] { DnsRecordType.SRV, new byte[] { 0, 1, 0, 2, 0, 80 }.Concat(quotedName).ToArray(), @"1 2 80 A\""B\""C.Example." };
        yield return new object[] { DnsRecordType.SOA, quotedName.Concat(quotedName).Concat(new byte[] { 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 4, 0, 0, 0, 5 }).ToArray(), @"A\""B\""C.Example. A\""B\""C.Example. 1 2 3 4 5" };
        yield return new object[] { DnsRecordType.NAPTR, new byte[] { 0, 1, 0, 2, 1, 117, 3, 115, 105, 112, 0 }.Concat(quotedName).ToArray(), @"1 2 ""u"" ""sip"" """" A\""B\""C.Example." };
        yield return new object[] { DnsRecordType.LOC, new byte[] { 0, 0x12, 0x16, 0x13, 128, 0, 0, 0, 128, 0, 0, 0, 0, 152, 150, 128 }, "0 N 0 E 0m" };
        foreach (DnsRecordType type in new[] { DnsRecordType.TXT, DnsRecordType.SPF })
            yield return new object[] { type, new byte[] { 2, 65, 66, 0, 2, 67, 68 }, "\"AB\" \"\" \"CD\"" };
        yield return new object[] { DnsRecordType.HINFO, new byte[] { 3, 67, 80, 85, 2, 79, 83 }, "\"CPU\" \"OS\"" };
        yield return new object[] { DnsRecordType.URI, new byte[] { 0, 10, 0, 20 }.Concat(Encoding.ASCII.GetBytes("https://Example/AbCd")).ToArray(), "10 20 \"https://Example/AbCd\"" };
        yield return new object[] { DnsRecordType.SSHFP, new byte[] { 4, 2, 171, 205 }, "4 2 ab cd" };
        foreach (DnsRecordType type in new[] { DnsRecordType.SVCB, DnsRecordType.HTTPS })
            yield return new object[] { type, new byte[] { 0, 1, 0, 0, 3, 0, 2, 1, 187 }, "1 . port=443" };
        yield return new object[] { DnsRecordType.NSEC3PARAM, new byte[] { 1, 0, 0, 2, 1, 171 }, "1 0 2 AB" };
        yield return new object[] { DnsRecordType.NSEC3, new byte[] { 1, 0, 0, 2, 1, 171, 1, 0, 0, 1, 64 }, "1 0 2 AB 00 A" };
        foreach (DnsRecordType type in new[] { DnsRecordType.RRSIG, DnsRecordType.SIG })
            yield return new object[] { type, new byte[] { 0, 1, 8, 2, 0, 0, 0, 60, 0, 0, 0, 3, 0, 0, 0, 2, 0, 1 }
                .Concat(name).Concat(new byte[] { 1, 2, 3 }).ToArray(), "A 8 2 60 3 2 1 Target.Example. AQID" };
    }

    private static string TypedValue(DnsAnswer answer) => answer.TypedRecord switch {
        ARecord ipv4 => ipv4.Address.ToString(),
        AAAARecord ipv6 => ipv6.Address.ToString(),
        TxtRecord txt => txt.Text,
        object record => JsonSerializer.Serialize(record),
        _ => "null"
    };

    private static byte[] Name(params string[] labels) => labels.SelectMany(label => new[] { (byte)label.Length }.Concat(Encoding.ASCII.GetBytes(label))).Concat(new byte[] { 0 }).ToArray();

    private static byte[] WireResponse(DnsRecordType type, byte[] rdata) => new byte[] { 0x12, 0x34, 0x81, 0x80, 0, 0, 0, 1, 0, 0, 0, 0 }
        .Concat(Name("example", "com")).Concat(new byte[] { (byte)((ushort)type >> 8), (byte)type, 0, 1, 0, 0, 0, 60, (byte)(rdata.Length >> 8), (byte)rdata.Length }).Concat(rdata).ToArray();
}
