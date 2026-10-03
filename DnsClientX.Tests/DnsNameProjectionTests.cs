using System.Text;

namespace DnsClientX.Tests;

/// <summary>Protects root markers and escaped label octets across public record projections.</summary>
public class DnsNameProjectionTests {
    /// <summary>Typed name fields remove only the root separator, retaining literal label dots.</summary>
    [Theory]
    [InlineData(DnsRecordType.CNAME)]
    [InlineData(DnsRecordType.NS)]
    [InlineData(DnsRecordType.PTR)]
    [InlineData(DnsRecordType.DNAME)]
    [InlineData(DnsRecordType.MX)]
    [InlineData(DnsRecordType.SOA)]
    [InlineData(DnsRecordType.SRV)]
    public void TypedNameFieldsPreserveLiteralDotsAndRoot(DnsRecordType type) {
        foreach (string name in new[] { @"literal\..", "." }) {
            string raw = type switch {
                DnsRecordType.MX => "10 " + name,
                DnsRecordType.SOA => name + " " + name + " 1 2 3 4 5",
                DnsRecordType.SRV => "1 2 443 " + name,
                _ => name
            };
            var answer = new DnsAnswer { Type = type, DataRaw = raw };
            string expected = name == "." ? "." : @"literal\.";
            object? record = answer.TypedRecord;
            string observed = record switch {
                CNameRecord cname => cname.CName,
                NsRecord ns => ns.Host,
                PtrRecord ptr => ptr.Pointer,
                DnameRecord dname => dname.Target,
                MxRecord mx => mx.Exchange,
                SoaRecord soa => soa.PrimaryNameServer,
                SrvRecord srv => srv.Target,
                _ => throw new InvalidOperationException("Expected a typed DNS name record.")
            };
            Assert.Equal(expected, observed);
            if (record is SoaRecord soaRecord) Assert.Equal(expected, soaRecord.ResponsiblePerson);
            Assert.Equal(raw, answer.DataRaw);
        }
    }

    /// <summary>Provider presentation, Base64, and hexadecimal NAPTR agree on the replacement name.</summary>
    [Theory]
    [InlineData("presentation")]
    [InlineData("base64")]
    [InlineData("hexadecimal")]
    public void NaptrReplacementPreservesLiteralDot(string encoding) {
        byte[] name = new byte[] { 8 }.Concat(Encoding.ASCII.GetBytes("literal.")).Concat(new byte[] { 0 }).ToArray();
        byte[] rdata = new byte[] { 0, 1, 0, 2, 0, 0, 0 }.Concat(name).ToArray();
        string raw = encoding switch {
            "base64" => Convert.ToBase64String(rdata),
            "hexadecimal" => "\\# " + rdata.Length + " " + string.Join(" ", rdata.Select(value => value.ToString("X2"))),
            _ => "1 2 \"\" \"\" \"\" literal\\.."
        };
        var answer = new DnsAnswer { Type = DnsRecordType.NAPTR, DataRaw = raw };
        Assert.Equal("1 2 \"\" \"\" \"\" literal\\.", answer.Data);
        Assert.Equal(@"literal\.", Assert.IsType<NaptrRecord>(answer.TypedRecord).Replacement);
    }

    /// <summary>Base64 PTR labels preserve dots, backslashes, and non-ASCII octets as DNS escapes.</summary>
    [Fact]
    public void BinaryPtrPreservesLabelOctets() {
        byte[] bytes = { 4, (byte)'A', (byte)'.', (byte)'\\', 255, 0 };
        var answer = new DnsAnswer { Type = DnsRecordType.PTR, DataRaw = Convert.ToBase64String(bytes) };
        Assert.Equal(@"a\.\\\255", answer.Data);
        Assert.Equal(answer.Data, Assert.IsType<PtrRecord>(answer.TypedRecord).Pointer);
    }

    /// <summary>Lightweight question inspection and full decoding expose the same presentation name.</summary>
    [Theory]
    [InlineData(".", ".")]
    [InlineData(@"literal\..", @"literal\.")]
    [InlineData(@"literal\\.", @"literal\\")]
    public async Task QuestionInspectionPreservesName(string name, string expected) {
        byte[] wire = new DnsMessage(name, DnsRecordType.PTR, requestDnsSec: false).SerializeDnsWireFormat();
        Assert.True(DnsWireMessageParser.TryParseQuestion(wire, 0, out var question));
        Assert.Equal(expected, question.Name);
        DnsResponse response = await DnsWire.DeserializeDnsWireFormat(null, false, wire);
        Assert.Equal(expected, response.Questions.Single().Name);
    }
}
