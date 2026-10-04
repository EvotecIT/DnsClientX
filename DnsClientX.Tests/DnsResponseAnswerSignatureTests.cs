namespace DnsClientX.Tests;

/// <summary>Protects complete RRset identity used by resolver consensus and recommendation.</summary>
public sealed class DnsResponseAnswerSignatureTests {
    /// <summary>Root owners and a literal final dot remain valid through public response grouping.</summary>
    [Theory]
    [InlineData(".", ".", ".")]
    [InlineData(@"literal\..", @"literal\.", @"literal\046.")]
    public void PreservesCompleteDnsOwners(string input, string expectedName, string equivalent) {
        var answer = new DnsAnswer { Name = input, Type = DnsRecordType.NS, DataRaw = "ns.example." };
        Assert.Equal(expectedName, answer.Name);
        var question = new DnsQuestion { Name = input };
        Assert.Equal(expectedName, question.Name);
        var response = new DnsResponse { Answers = [answer] };
        var other = new DnsResponse { Answers = [new DnsAnswer { Name = equivalent, Type = answer.Type, DataRaw = answer.DataRaw }] };
        Assert.Equal(DnsResponseAnswerSignature.Build(other), DnsResponseAnswerSignature.Build(response));
    }

    /// <summary>Address spelling cannot split otherwise equal resolver consensus groups.</summary>
    [Fact]
    public void EquivalentIpv6PresentationsHaveOneSignature() {
        var first = new DnsResponse { Answers = [new DnsAnswer { Name = "example.com", Type = DnsRecordType.AAAA, DataRaw = "2001:0DB8:0:0:0:0:0:1" }] };
        var second = new DnsResponse { Answers = [new DnsAnswer { Name = "example.com", Type = DnsRecordType.AAAA, DataRaw = "2001:db8::1" }] };
        Assert.Equal("2001:db8::1", first.Answers[0].Data);
        Assert.Equal(DnsResponseAnswerSignature.Build(first), DnsResponseAnswerSignature.Build(second));
    }

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
