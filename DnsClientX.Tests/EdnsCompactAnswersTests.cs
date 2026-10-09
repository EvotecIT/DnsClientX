namespace DnsClientX.Tests;

/// <summary>Protects hop-by-hop compact-answer negotiation and response evidence.</summary>
public class EdnsCompactAnswersTests {
    /// <summary>Shared DNSSEC queries advertise CO; callers can explicitly disable negotiation.</summary>
    [Theory]
    [InlineData(true, true, 0xc000u)]
    [InlineData(true, false, 0x8000u)]
    [InlineData(false, true, 0u)]
    public void SharedQueriesNegotiateCompactAnswers(bool dnssec, bool compact, uint expected) {
        using var client = new ClientX(DnsEndpoint.Cloudflare);
        var edns = new EdnsOptions { CompactAnswersOk = compact };
        var configuration = client.EndpointConfiguration;
        configuration.EdnsOptions = edns;
        var query = DnsWireQueryBuilder.BuildQuery("example.com", DnsRecordType.A, dnssec, configuration);
        byte[] bytes = query.SerializeDnsWireFormat();
        int opt = bytes.Length - 11;
        uint ttl = ((uint)bytes[opt + 5] << 24) | ((uint)bytes[opt + 6] << 16) | ((uint)bytes[opt + 7] << 8) | bytes[opt + 8];
        Assert.Equal(expected, ttl);
        Assert.Equal(compact, edns.Clone().CompactAnswersOk);
    }

    /// <summary>Low-level message callers retain their DO-only default and opt in explicitly.</summary>
    [Fact]
    public void MessageOptionsAllowExplicitCompactNegotiation() {
        var options = new DnsMessageOptions(RequestDnsSec: true) { CompactAnswersOk = true };
        byte[] bytes = new DnsMessage("example.com", DnsRecordType.A, options).SerializeDnsWireFormat();
        Assert.Equal(0xc0, bytes[bytes.Length - 4]);
        Assert.Equal(0x80, new DnsMessage("example.com", DnsRecordType.A, true).SerializeDnsWireFormat()[bytes.Length - 4]);
    }

    /// <summary>Responses retain observed CO independently of authentication and cached status.</summary>
    [Fact]
    public async Task ResponseRetainsObservedCompactFlag() {
        byte[] bytes = new byte[] { 0,1,0x81,0x80,0,0,0,0,0,0,0,1,0,0,41,4,0xd0,0,0,0xc0,0,0,0 };
        var response = await DnsWire.DeserializeDnsWireFormat(null, false, bytes);
        Assert.True(response.EdnsCompactAnswersOk);
        Assert.True(response.EdnsDnsSecOk);
        Assert.False(response.DnsSecCompactDenial);
        Assert.Equal(DnsResponseCode.NoError, response.EffectiveStatus);
        Assert.True(response.Clone().EdnsCompactAnswersOk);
    }

    /// <summary>Provider JSON cannot assign a local validation or compact-denial verdict.</summary>
    [Fact]
    public void ProviderJsonCannotClaimLocalCompactDenial() {
        var response = System.Text.Json.JsonSerializer.Deserialize<DnsResponse>(
            "{\"Status\":0,\"AD\":true,\"dnssec_validation_status\":1,\"dnssec_compact_denial\":true,\"effective_status\":3}")!;
        Assert.True(response.AuthenticData);
        Assert.False(response.DnsSecCompactDenial);
        Assert.Equal(DnsSecValidationStatus.NotRequested, response.DnsSecValidationStatus);
        Assert.Equal(DnsResponseCode.NoError, response.EffectiveStatus);
    }

    /// <summary>Different CO requests do not share cached resolver responses.</summary>
    [Fact]
    public void NegotiationIsPartOfCacheIdentity() {
        using var client = new ClientX(DnsEndpoint.Cloudflare);
        var configuration = client.EndpointConfiguration;
        var edns = new EdnsOptions();
        configuration.EdnsOptions = edns;
        string enabled = Key(configuration);
        edns.CompactAnswersOk = false;
        Assert.NotEqual(enabled, Key(configuration));
    }

    private static string Key(Configuration configuration) => DnsCacheKeyBuilder.Build(configuration,
        "example.com", DnsRecordType.A, true, true, false, false, false, TimeSpan.FromMinutes(5), false);
}
