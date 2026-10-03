using System.Text;

namespace DnsClientX.Tests;

/// <summary>Protects binary provider representations and character-string values in public projections.</summary>
public class GenericRdataProjectionTests {
    /// <summary>Valid RFC 3597 data accepts contiguous or whitespace-separated hexadecimal octets.</summary>
    [Theory]
    [InlineData(DnsRecordType.CAA, false)]
    [InlineData(DnsRecordType.CAA, true)]
    [InlineData(DnsRecordType.NAPTR, false)]
    [InlineData(DnsRecordType.NAPTR, true)]
    [InlineData(DnsRecordType.TLSA, false)]
    [InlineData(DnsRecordType.TLSA, true)]
    public void HexadecimalRecordsPreserveTheirFields(DnsRecordType type, bool separated) {
        byte[] bytes = type switch {
            DnsRecordType.CAA => new byte[] { 128, 5 }.Concat(Encoding.ASCII.GetBytes("issueCA.Example")).ToArray(),
            DnsRecordType.NAPTR => new byte[] { 0, 1, 0, 2, 1, (byte)'u', 0, 0, 1, (byte)'x', 0 },
            _ => new byte[] { 3, 1, 1, 0xAB, 0xCD }
        };
        string hex = string.Join(separated ? " " : "", bytes.Select(value => value.ToString("X2")));
        string raw = "\\# " + bytes.Length + " " + hex;
        var answer = new DnsAnswer { Type = type, DataRaw = raw };
        switch (type) {
            case DnsRecordType.CAA:
                Assert.Equal("128 issue \"CA.Example\"", answer.Data);
                var caa = Assert.IsType<CaaRecord>(answer.TypedRecord);
                Assert.Equal((byte)128, caa.Flags);
                Assert.Equal("CA.Example", caa.Value);
                break;
            case DnsRecordType.NAPTR:
                Assert.Equal("1 2 \"u\" \"\" \"\" x", answer.Data);
                Assert.Equal("x", Assert.IsType<NaptrRecord>(answer.TypedRecord).Replacement);
                break;
            default:
                Assert.Equal("3 1 1 ABCD", answer.Data);
                Assert.IsType<TlsaRecord>(answer.TypedRecord);
                break;
        }
        Assert.Equal(raw, answer.DataRaw);
    }

    /// <summary>TLSA association bytes have the same hexadecimal projection across provider representations.</summary>
    [Theory]
    [InlineData("3 1 1 abcd")]
    [InlineData("3 1 1 ABCD")]
    [InlineData("AwEBq80=")]
    public void TlsaAssociationHexIsCanonical(string raw) {
        var answer = new DnsAnswer { Type = DnsRecordType.TLSA, DataRaw = raw };
        Assert.Equal("3 1 1 ABCD", answer.Data);
        Assert.IsType<TlsaRecord>(answer.TypedRecord);
        Assert.Equal(raw, answer.DataRaw);
    }

    /// <summary>Typed CAA removes presentation quoting without changing escaped value octets.</summary>
    [Fact]
    public void TypedCaaPreservesEscapedValue() {
        var answer = new DnsAnswer { Type = DnsRecordType.CAA, DataRaw = "128 custom \"A\\\"\\\\B\\255\"" };
        CaaRecord record = Assert.IsType<CaaRecord>(answer.TypedRecord);
        Assert.Equal("A\"\\B\u00ff", record.Value);
        Assert.Equal((byte)128, record.Flags);
    }

    /// <summary>Malformed generic data remains inspectable without decoding a partial record or throwing.</summary>
    [Theory]
    [InlineData(DnsRecordType.CAA, "\\# 2 80056973737565")]
    [InlineData(DnsRecordType.NAPTR, "\\# 2 0001000200000000")]
    [InlineData(DnsRecordType.TLSA, "\\# 4 030101 GG")]
    [InlineData(DnsRecordType.TLSA, "AAE=")]
    public void MalformedEncodingIsNotReinterpreted(DnsRecordType type, string raw) {
        var answer = new DnsAnswer { Type = type, DataRaw = raw };
        Assert.Equal(raw, answer.Data);
        Assert.Equal(raw, Assert.IsType<UnknownRecord>(answer.TypedRecord).Data);
    }
}
