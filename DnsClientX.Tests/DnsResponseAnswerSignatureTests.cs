namespace DnsClientX.Tests;

/// <summary>Protects complete RRset identity used by resolver consensus and recommendation.</summary>
public sealed class DnsResponseAnswerSignatureTests {
    /// <summary>TXT payload delimiters cannot impersonate another record boundary.</summary>
    [Fact]
    public void DistinguishesSinglePayloadFromMultipleRecords() {
        var single = new DnsResponse { Answers = [new DnsAnswer { Name = "example.com", Type = DnsRecordType.TXT, DataRaw = "\"first;example.com|TXT|second\"" }] };
        var multiple = new DnsResponse { Answers = [
            new DnsAnswer { Name = "example.com", Type = DnsRecordType.TXT, DataRaw = "\"first\"" },
            new DnsAnswer { Name = "example.com", Type = DnsRecordType.TXT, DataRaw = "\"second\"" }
        ] };
        Assert.NotEqual(DnsResponseAnswerSignature.Build(single), DnsResponseAnswerSignature.Build(multiple));
        var reversed = new DnsResponse { Answers = [multiple.Answers[1], multiple.Answers[0]] };
        Assert.Equal(DnsResponseAnswerSignature.Build(multiple), DnsResponseAnswerSignature.Build(reversed));
    }
}
