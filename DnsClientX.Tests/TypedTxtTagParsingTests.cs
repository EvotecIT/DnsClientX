namespace DnsClientX.Tests;

/// <summary>Malformed specialized TXT content must remain usable through generic typed answers.</summary>
public class TypedTxtTagParsingTests {
    /// <summary>Case-insensitive duplicate tags cannot throw during a network answer's typed projection.</summary>
    [Theory]
    [InlineData("v=DKIM1; p=AbCd; P=EfGh", true)]
    [InlineData("v=DMARC1; p=none; P=reject", false)]
    public void DuplicateTagsUseGenericFallback(string raw, bool dkim) {
        Assert.False(dkim ? DkimRecord.TryParse(raw, out _) : DmarcRecord.TryParse(raw, out _));
        var answer = new DnsAnswer { Type = DnsRecordType.TXT, DataRaw = raw };
        var generic = Assert.IsType<KeyValueTxtRecord>(DnsRecordFactory.Create(answer, parseTypedTxtRecords: true));
        Assert.Equal(3, generic.Tags.Length);
        Assert.Equal(raw, answer.Data);
        Assert.Equal(raw, answer.DataRaw);
    }

    /// <summary>Tag spacing is syntax; payload case and embedded equals signs are content.</summary>
    [Theory]
    [InlineData("v=DKIM1; p = AbCd==", true)]
    [InlineData("v=DMARC1; rua = mailto:First.Last@Example.com", false)]
    public void TagsKeepPayloadAndNormalizeNames(string raw, bool dkim) {
        var answer = new DnsAnswer { Type = DnsRecordType.TXT, DataRaw = raw };
        object typed = DnsRecordFactory.Create(answer, parseTypedTxtRecords: true)!;
        if (dkim) Assert.Equal("AbCd==", Assert.IsType<DkimRecord>(typed).Tags["p"]);
        else Assert.Equal("mailto:First.Last@Example.com", Assert.IsType<DmarcRecord>(typed).Tags["rua"]);
        Assert.Equal(raw, answer.Data);
    }
}
