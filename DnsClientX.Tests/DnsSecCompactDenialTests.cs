using System.Net.Http;

namespace DnsClientX.Tests;

/// <summary>Exercises RFC 9824 denials through real signatures and the complete trust chain.</summary>
public class DnsSecCompactDenialTests {
    private const string Name = "missing.example.com";

    /// <summary>Authenticated NSEC and NSEC3 normalize both permitted header codes to name error.</summary>
    [Theory]
    [InlineData(false, DnsResponseCode.NoError)]
    [InlineData(false, DnsResponseCode.NXDomain)]
    [InlineData(true, DnsResponseCode.NoError)]
    [InlineData(true, DnsResponseCode.NXDomain)]
    public async Task CompactDenialUsesAuthenticatedProof(bool nsec3, DnsResponseCode status) {
        using var fixture = new DnsSecSignedFixture();
        var response = Compact(fixture, nsec3);
        response.Status = status;
        Assert.False(response.DnsSecCompactDenial);
        Assert.Equal(status, response.EffectiveStatus);
        var engine = fixture.Engine();
        var result = await engine.ValidateAsync(response, Name, DnsRecordType.A, default);
        response.DnsSecValidationStatus = result.Status;
        response.DnsSecValidationExpiresUtc = engine.CacheExpiresAtUtc;
        Assert.Equal(DnsSecValidationStatus.Secure, result.Status);
        Assert.True(response.DnsSecCompactDenial);
        Assert.Equal(status, response.Status);
        Assert.Equal(DnsResponseCode.NXDomain, response.EffectiveStatus);
        Assert.True(response.DnsSecValidationExpiresUtc <= fixture.Now.AddHours(1));

        using var cache = new DnsResponseCache();
        cache.Set(Name + ":A", response, TimeSpan.FromHours(2));
        response.DnsSecCompactDenial = false;
        Assert.True(cache.TryGet(Name + ":A", out var cached));
        Assert.True(cached.DnsSecCompactDenial);
        Assert.Equal(DnsResponseCode.NXDomain, cached.EffectiveStatus);
        Assert.False(cache.TryGet("child." + Name + ":A", out _));
        Assert.False(cache.TryGet(Name + ":AAAA", out _));
    }

    /// <summary>A signed empty non-terminal remains NODATA rather than NXDOMAIN.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyNonTerminalIsNotANameError(bool nsec3) {
        using var fixture = new DnsSecSignedFixture();
        var response = nsec3 ? fixture.CompactNsec3(Name)
            : fixture.Nsec(Name, "\\000." + Name, DnsRecordType.NSEC, DnsRecordType.RRSIG);
        var result = await fixture.Engine().ValidateAsync(response, Name, DnsRecordType.A, default);
        response.DnsSecValidationStatus = result.Status;
        Assert.Equal(DnsSecValidationStatus.Secure, result.Status);
        Assert.False(response.DnsSecCompactDenial);
        Assert.Equal(DnsResponseCode.NoError, response.EffectiveStatus);
    }

    /// <summary>Tampered data cannot set a compact-denial verdict or alter the observed status.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TamperedProofIsNotNormalized(bool nsec3) {
        using var fixture = new DnsSecSignedFixture();
        var response = Compact(fixture, nsec3);
        response.WireMessage[response.WireAuthorities[0].RdataLength - 1] ^= 1;
        var result = await fixture.Engine().ValidateAsync(response, Name, DnsRecordType.A, default);
        response.DnsSecValidationStatus = result.Status;
        Assert.Equal(DnsSecValidationStatus.Bogus, result.Status);
        Assert.False(response.DnsSecCompactDenial);
        Assert.Equal(DnsResponseCode.NoError, response.EffectiveStatus);
    }

    /// <summary>NXNAME with extra type bits or an unrelated owner cannot fall back to NODATA.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InvalidCompactShapesAreNotSecure(bool nsec3, bool wrongOwner) {
        using var fixture = new DnsSecSignedFixture();
        string owner = wrongOwner ? "other.example.com" : Name;
        var response = nsec3
            ? fixture.CompactNsec3(owner, wrongOwner ? new[] { DnsRecordType.NXNAME } : new[] { DnsRecordType.NXNAME, DnsRecordType.AAAA })
            : fixture.Nsec(owner, "\\000." + owner, wrongOwner
                ? new[] { DnsRecordType.NXNAME, DnsRecordType.NSEC, DnsRecordType.RRSIG }
                : new[] { DnsRecordType.NXNAME, DnsRecordType.NSEC, DnsRecordType.RRSIG, DnsRecordType.AAAA });
        var result = await fixture.Engine().ValidateAsync(response, Name, DnsRecordType.A, default);
        response.DnsSecValidationStatus = result.Status;
        Assert.NotEqual(DnsSecValidationStatus.Secure, result.Status);
        Assert.False(response.DnsSecCompactDenial);
        Assert.Equal(DnsResponseCode.NoError, response.EffectiveStatus);
    }

    /// <summary>A signed alias must authenticate the exact final target's compact denial.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AliasAuthenticatesItsFinalTarget(bool nsec3) {
        using var fixture = new DnsSecSignedFixture();
        var alias = fixture.Signed("alias.example.com", DnsRecordType.CNAME, DnsWireNameCodec.ToCanonicalWire(Name));
        DnsSecSignedFixture.WithProofs(alias, Compact(fixture, nsec3));
        var result = await fixture.Engine().ValidateAsync(alias, "alias.example.com", DnsRecordType.A, default);
        alias.DnsSecValidationStatus = result.Status;
        Assert.Equal(DnsSecValidationStatus.Secure, result.Status);
        Assert.True(alias.DnsSecCompactDenial);
        Assert.Equal(DnsResponseCode.NXDomain, alias.EffectiveStatus);
    }

    /// <summary>Compact name errors cannot coexist with a signed positive RRset at the final owner.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompactDenialRejectsPositiveFinalOwner(bool nsec3) {
        using var fixture = new DnsSecSignedFixture();
        var answer = fixture.Signed("alias.example.com", DnsRecordType.CNAME, DnsWireNameCodec.ToCanonicalWire(Name));
        var positive = fixture.Signed(Name, DnsRecordType.A, new byte[] { 192, 0, 2, 3 });
        int offset = answer.WireMessage.Length;
        answer.WireMessage = answer.WireMessage.Concat(positive.WireMessage).ToArray();
        answer.WireAnswers = answer.WireAnswers.Concat(positive.WireAnswers.Select(record => new DnsWireResourceRecord(
            record.Name, record.Type, record.Class, record.Ttl, record.RawTtl, record.RdataOffset + offset, record.RdataLength, record.Data))).ToArray();
        answer.Answers = answer.Answers.Concat(positive.Answers).ToArray();
        DnsSecSignedFixture.WithProofs(answer, Compact(fixture, nsec3));
        Assert.Equal(DnsSecValidationStatus.Bogus,
            (await fixture.Engine().ValidateAsync(answer, "alias.example.com", DnsRecordType.AAAA, default)).Status);
        Assert.Equal(DnsSecValidationStatus.Bogus,
            (await fixture.Engine().ValidateAliasAsync(answer, "alias.example.com", DnsRecordType.AAAA, default)).Status);
        Assert.False(answer.DnsSecCompactDenial);
    }

    /// <summary>Compact proofs retain strict parent-side DS and non-wildcard denial boundaries.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CompactDenialCannotBypassSignerBoundaries(bool nsec3, bool wildcard) {
        using var fixture = new DnsSecSignedFixture();
        string name = wildcard ? Name : fixture.Zone.Name;
        var proof = nsec3 ? fixture.CompactNsec3(name, DnsRecordType.NXNAME)
            : fixture.Nsec(name, "\\000." + name, DnsRecordType.NSEC, DnsRecordType.RRSIG, DnsRecordType.NXNAME);
        if (wildcard) {
            var record = proof.WireAuthorities[0];
            proof = fixture.Signed(record.Name, record.Type,
                proof.WireMessage.Skip(record.RdataOffset).Take(record.RdataLength).ToArray(), labels: 1, authority: true);
        }
        var result = await fixture.Engine().ValidateAsync(proof, name, wildcard ? DnsRecordType.A : DnsRecordType.DS, default);
        proof.DnsSecValidationStatus = result.Status;
        Assert.NotEqual(DnsSecValidationStatus.Secure, result.Status);
        Assert.False(proof.DnsSecCompactDenial);
        Assert.Equal(DnsResponseCode.NoError, proof.EffectiveStatus);
    }

    /// <summary>A zero TTL permits authentication but prevents caching the resulting verdict.</summary>
    [Fact]
    public async Task ZeroTtlProofIsAuthenticatedButNotCached() {
        using var fixture = new DnsSecSignedFixture { MaterialTtl = 0 };
        var response = Compact(fixture, false);
        var engine = fixture.Engine();
        var result = await engine.ValidateAsync(response, Name, DnsRecordType.A, default);
        response.DnsSecValidationStatus = result.Status;
        response.DnsSecValidationExpiresUtc = engine.CacheExpiresAtUtc;
        Assert.Equal(DnsSecValidationStatus.Secure, result.Status);
        Assert.True(response.DnsSecCompactDenial);
        Assert.Equal(DnsResponseCode.NXDomain, response.EffectiveStatus);
        using var cache = new DnsResponseCache();
        cache.Set(Name + ":A", response, TimeSpan.FromHours(1));
        Assert.False(cache.TryGet(Name + ":A", out _));
    }

    /// <summary>NXNAME queries are rejected before normal, iterative, JSON or wire forwarding.</summary>
    [Fact]
    public async Task NxNameCannotBeQueried() {
        Assert.Throws<ArgumentException>(() => new DnsMessage(Name, DnsRecordType.NXNAME, false));
        using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP);
        await Assert.ThrowsAsync<ArgumentException>(() => client.Resolve(Name, DnsRecordType.NXNAME));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ResolveFromRoot(Name, DnsRecordType.NXNAME));
        using var http = new HttpClient(new NoNetworkHandler());
        await Assert.ThrowsAsync<ArgumentException>(() => DnsJsonQueryClient.QueryAsync(http, new Uri("https://example.com/resolve"), Name, DnsRecordType.NXNAME));
    }

    private static DnsResponse Compact(DnsSecSignedFixture fixture, bool nsec3) => nsec3
        ? fixture.CompactNsec3(Name, DnsRecordType.NXNAME)
        : fixture.Nsec(Name, "\\000." + Name, DnsRecordType.NSEC, DnsRecordType.RRSIG, DnsRecordType.NXNAME);

    private sealed class NoNetworkHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An NXNAME query reached HTTP forwarding.");
    }
}
