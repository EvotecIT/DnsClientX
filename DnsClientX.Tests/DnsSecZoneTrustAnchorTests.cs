using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Authenticates private islands using real RSA signatures and exact configured DS material.</summary>
public class DnsSecZoneTrustAnchorTests {
    /// <summary>Configured apex keys require no parent DS and bound both record and proof lifetime.</summary>
    [Fact]
    public async Task ExactZoneAuthenticatesWithoutParentAndExpiresWithItsAnchor() {
        using var fixture = new DnsSecSignedFixture();
        var anchor = Anchor(fixture, until: fixture.Now.AddSeconds(20));
        var engine = Engine(fixture, new[] { anchor });
        DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 9 });
        Assert.Equal(DnsSecValidationStatus.Secure, (await engine.ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
        Assert.Equal(anchor.Anchor.ValidUntil, engine.CacheExpiresAtUtc);
        Assert.Equal(20, answer.Answers[0].TTL);
        // An engine starts with a fresh snapshot and must reject the expired exact boundary.
        var expired = new DnsSecValidationEngine((name, type, token) => fixture.LookupAsync(name, type, token),
            fixture.Now.AddSeconds(21), zoneTrustAnchors: new[] { anchor });
        Assert.Equal(DnsSecValidationStatus.Bogus, (await expired.ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
    }

    /// <summary>Invalid configured trust never uses the public parent chain to recover a Secure verdict.</summary>
    [Theory]
    [InlineData("digest", DnsSecValidationStatus.Bogus)]
    [InlineData("expired", DnsSecValidationStatus.Bogus)]
    [InlineData("future", DnsSecValidationStatus.Bogus)]
    [InlineData("revoked", DnsSecValidationStatus.Bogus)]
    [InlineData("algorithm", DnsSecValidationStatus.Indeterminate)]
    [InlineData("digest-type", DnsSecValidationStatus.Indeterminate)]
    public async Task InvalidExactAnchorFailsClosed(string kind, DnsSecValidationStatus expected) {
        using var fixture = new DnsSecSignedFixture();
        DnsSecTrustAnchor baseline = Anchor(fixture).Anchor;
        string digest = kind == "digest" ? new string('0', 64) : baseline.Digest;
        var anchor = new DnsSecZoneTrustAnchor(fixture.Zone.Name, new DnsSecTrustAnchor(baseline.KeyTag,
            kind == "algorithm" ? (DnsKeyAlgorithm)253 : baseline.Algorithm, kind == "digest-type" ? (byte)250 : baseline.DigestType,
            digest, kind == "future" ? fixture.Now.AddMinutes(1) : null,
            kind == "expired" ? fixture.Now.AddSeconds(-1) : null, flags: kind == "revoked" ? (ushort)385 : (ushort)257));
        DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 9 });
        Assert.Equal(expected, (await Engine(fixture, new[] { anchor }).ValidateAsync(answer, "www.example.com", DnsRecordType.A, default)).Status);
    }

    /// <summary>Keys below an anchored apex still require parent-authenticated DS and their own DNSKEY signatures.</summary>
    [Fact]
    public async Task DescendantDelegationAuthenticatesThroughTheConfiguredParent() {
        using var fixture = new DnsSecSignedFixture();
        var child = new DnsSecKey("child.example.com", 257, 3, 8, fixture.Zone.PublicKey);
        Assert.True(DnsSecCrypto.TryComputeDsDigest(child.Name, child, 2, out byte[] digest));
        byte[] ds = new[] { (byte)(child.KeyTag >> 8), (byte)child.KeyTag, child.Algorithm, (byte)2 }.Concat(digest).ToArray();
        DnsResponse dsReply = fixture.Signed(child.Name, DnsRecordType.DS, ds, key: fixture.Zone);
        DnsResponse keys = fixture.Signed(child.Name, DnsRecordType.DNSKEY, new byte[] { 1, 1, 3, 8 }.Concat(child.PublicKey).ToArray(), key: child);
        var engine = Engine(fixture, new[] { Anchor(fixture) }, (name, type) => name == child.Name ? type == DnsRecordType.DS ? dsReply : keys : null);
        DnsResponse answer = fixture.Signed("www.child.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 10 }, key: child);
        Assert.Equal(DnsSecValidationStatus.Secure, (await engine.ValidateAsync(answer, "www.child.example.com", DnsRecordType.A, default)).Status);
        dsReply.WireMessage[4] ^= 1;
        engine = Engine(fixture, new[] { Anchor(fixture) }, (name, type) => name == child.Name ? type == DnsRecordType.DS ? dsReply : keys : null);
        Assert.Equal(DnsSecValidationStatus.Bogus, (await engine.ValidateAsync(answer, "www.child.example.com", DnsRecordType.A, default)).Status);
    }

    /// <summary>An ancestor's valid signature cannot bypass a more specific configured private zone.</summary>
    [Fact]
    public async Task AncestorSignaturesAndUnsignedApexCannotBypassPrivateBoundary() {
        using var fixture = new DnsSecSignedFixture();
        DnsResponse parentSigned = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 9 }, key: fixture.Root);
        Assert.Equal(DnsSecValidationStatus.Bogus, (await Engine(fixture, new[] { Anchor(fixture) }).ValidateAsync(parentSigned, "www.example.com", DnsRecordType.A, default)).Status);
        DnsResponse unsigned = fixture.Signed("example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 9 });
        unsigned.WireAnswers = unsigned.WireAnswers.Where(record => record.Type != DnsRecordType.RRSIG).ToArray();
        Assert.Equal(DnsSecValidationStatus.Bogus, (await Engine(fixture, new[] { Anchor(fixture) }).ValidateAsync(unsigned, "example.com", DnsRecordType.A, default)).Status);
    }

    /// <summary>Private-zone denial requires its own authenticated proof and key signatures.</summary>
    [Fact]
    public async Task PrivateDenialAndAnchorKeySignaturesAreVerifiedCryptographically() {
        using var fixture = new DnsSecSignedFixture();
        var anchor = Anchor(fixture);
        DnsResponse denial = fixture.Nsec("www.example.com", "zzz.example.com", DnsRecordType.NSEC, DnsRecordType.RRSIG);
        Assert.Equal(DnsSecValidationStatus.Secure, (await Engine(fixture, new[] { anchor }).ValidateAsync(denial, "www.example.com", DnsRecordType.A, default)).Status);
        DnsResponse keys = await fixture.LookupAsync(fixture.Zone.Name, DnsRecordType.DNSKEY, default);
        keys.WireMessage[keys.WireMessage.Length - 1] ^= 1;
        var engine = Engine(fixture, new[] { anchor }, (_, type) => type == DnsRecordType.DNSKEY ? keys : null);
        Assert.Equal(DnsSecValidationStatus.Bogus, (await engine.ValidateAsync(denial, "www.example.com", DnsRecordType.A, default)).Status);
    }

    /// <summary>Most-specific anchors define the boundary while public DS remains authenticated on the parent side.</summary>
    [Fact]
    public async Task MoreSpecificAnchorOverridesParentAndPublicDsRetainsParentSideSemantics() {
        using var fixture = new DnsSecSignedFixture();
        var broken = new DnsSecZoneTrustAnchor("child.example.com", new DnsSecTrustAnchor(1, DnsKeyAlgorithm.RSASHA256, 2, new string('0', 64)));
        DnsResponse answer = fixture.Signed("www.child.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 9 });
        Assert.Equal(DnsSecValidationStatus.Bogus, (await Engine(fixture, new[] { Anchor(fixture), broken }).ValidateAsync(answer, "www.child.example.com", DnsRecordType.A, default)).Status);
        DnsResponse ds = await fixture.LookupAsync(fixture.Zone.Name, DnsRecordType.DS, default);
        var engine = new DnsSecValidationEngine((name, type, token) => fixture.LookupAsync(name, type, token), fixture.Now,
            trustAnchorStorePath: fixture.AnchorPath, zoneTrustAnchors: new[] { Anchor(fixture) });
        Assert.Equal(DnsSecValidationStatus.Secure, (await engine.ValidateAsync(ds, fixture.Zone.Name, DnsRecordType.DS, default)).Status);
    }

    /// <summary>An ancestor DNAME cannot redirect a name across an explicitly configured child trust boundary.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task AncestorDnameCannotBypassPrivateBoundary(bool synthesizedCname, bool negative, bool aliasOnly) {
        using var fixture = new DnsSecSignedFixture();
        const string query = "www.child.old.example.com";
        const string target = "www.child.new.example.com";
        DnsResponse answer = fixture.Signed("old.example.com", DnsRecordType.DNAME,
            DnsWireNameCodec.ToCanonicalWire("new.example.com"));
        if (synthesizedCname) {
            byte[] targetWire = DnsWireNameCodec.ToCanonicalWire(target);
            int offset = answer.WireMessage.Length;
            answer.WireMessage = answer.WireMessage.Concat(targetWire).ToArray();
            answer.WireAnswers = answer.WireAnswers.Concat(new[] {
                new DnsWireResourceRecord(query, DnsRecordType.CNAME, 1, 3600, 3600,
                    offset, (ushort)targetWire.Length, target)
            }).ToArray();
        }
        if (negative) {
            DnsSecSignedFixture.WithProofs(answer,
                fixture.Nsec3("example.com", "example.com", false, DnsRecordType.SOA));
            answer.Status = DnsResponseCode.NXDomain;
        } else if (!aliasOnly) {
            DnsResponse terminal = fixture.Signed(target, DnsRecordType.A, new byte[] { 192, 0, 2, 9 });
            int offset = answer.WireMessage.Length;
            answer.WireMessage = answer.WireMessage.Concat(terminal.WireMessage).ToArray();
            answer.WireAnswers = answer.WireAnswers.Concat(terminal.WireAnswers.Select(record =>
                new DnsWireResourceRecord(record.Name, record.Type, record.Class, record.Ttl,
                    record.RawTtl, record.RdataOffset + offset, record.RdataLength, record.Data))).ToArray();
        }
        async Task<DnsSecValidationResult> Validate(DnsSecValidationEngine validator) => aliasOnly
            ? await validator.ValidateAliasAsync(answer, query, DnsRecordType.A, default)
            : await validator.ValidateAsync(answer, query, DnsRecordType.A, default);

        Assert.Equal(DnsSecValidationStatus.Secure, (await Validate(Engine(fixture, new[] { Anchor(fixture) }))).Status);
        var child = new DnsSecZoneTrustAnchor("child.old.example.com",
            new DnsSecTrustAnchor(1, DnsKeyAlgorithm.RSASHA256, 2, new string('0', 64)));
        Assert.Equal(DnsSecValidationStatus.Bogus,
            (await Validate(Engine(fixture, new[] { Anchor(fixture), child }))).Status);
    }

    /// <summary>Snapshots are immutable and cache identity changes with zone, digest or validity policy.</summary>
    [Fact]
    public void ConfigurationSnapshotsAndCacheKeysBindAnchorPolicy() {
        using var fixture = new DnsSecSignedFixture();
        DnsSecZoneTrustAnchor anchor = Anchor(fixture);
        var list = new List<DnsSecZoneTrustAnchor> { anchor };
        var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { DnsSecTrustAnchors = list };
        Configuration snapshot = configuration.CreateQuerySnapshot("www.example.com");
        list.Clear();
        Assert.Same(anchor, Assert.Single(snapshot.DnsSecTrustAnchors!));
        string baseline = CacheKey(snapshot);
        Assert.NotEqual(baseline, CacheKey(configuration));
        configuration.DnsSecTrustAnchors = new[] { Anchor(fixture, until: fixture.Now.AddHours(1)) };
        Assert.NotEqual(baseline, CacheKey(configuration));
        configuration.DnsSecTrustAnchors = new[] { new DnsSecZoneTrustAnchor("other.example.com", anchor.Anchor) };
        Assert.NotEqual(baseline, CacheKey(configuration));
        Assert.Equal(DnsSecZoneTrustAnchor.CacheIdentity(new[] { anchor, anchor }), DnsSecZoneTrustAnchor.CacheIdentity(new[] { anchor }));
        list.Add(anchor);
        var run = new ResolverQueryRunOptions { DnsSecTrustAnchors = list };
        var execution = new ResolverExecutionClientOptions { DnsSecTrustAnchors = list };
        var request = new ResolveDnsRequest { DnsSecTrustAnchors = list };
        ResolveDnsRequest requestSnapshot = request.CreateTrustPolicySnapshot();
        list.Clear(); request.DnsSecTrustAnchors = Array.Empty<DnsSecZoneTrustAnchor>();
        Assert.Same(anchor, Assert.Single(run.DnsSecTrustAnchors!));
        Assert.Same(anchor, Assert.Single(execution.DnsSecTrustAnchors!));
        Assert.Same(anchor, Assert.Single(requestSnapshot.DnsSecTrustAnchors!));
    }

    /// <summary>Both recursive and iterative clients authenticate actual signed wire responses without parent lookups.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task HighLevelWireClientsAuthenticateAnExplicitIsland(int entryPoint) {
        using var fixture = new DnsSecSignedFixture();
        var anchor = Anchor(fixture, until: fixture.Now.AddSeconds(30));
        DnsResponse keys = await fixture.LookupAsync(fixture.Zone.Name, DnsRecordType.DNSKEY, default);
        DnsResponse answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 9 });
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stop = deadline.Token.Register(udp.Dispose);
        var observed = new List<(string Name, DnsRecordType Type)>();
        Task server = Serve();
        using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: 2000, enableCache: true);
        client.EndpointConfiguration.Port = port;
        client.EndpointConfiguration.EnableQNameMinimization = false;
        client.EndpointConfiguration.DnsSecTrustAnchors = new[] { anchor };
        try {
            DnsResponse result;
            if (entryPoint == 1) result = await client.ResolveFromRoot("www.example.com", DnsRecordType.A, new[] { "127.0.0.1" }, 10, port, true, true, deadline.Token);
            else if (entryPoint == 2) result = Assert.Single(await ClientX.QueryDns(new ResolveDnsRequest {
                Names = new[] { "www.example.com" }, Configuration = client.EndpointConfiguration, RequestDnsSec = true, ValidateDnsSec = true
            }, deadline.Token));
            else if (entryPoint == 3) result = Assert.Single(await ClientX.QueryDns(new ResolveDnsRequest {
                Names = new[] { "www.example.com" }, Servers = new[] { "127.0.0.1" }, Port = port, RequestFormat = DnsRequestFormat.DnsOverUDP,
                DnsSecTrustAnchors = new[] { anchor }, RequestDnsSec = true, ValidateDnsSec = true
            }, deadline.Token));
            else if (entryPoint == 4) {
                using var resolver = new DnsMultiResolver(new[] { new DnsResolverEndpoint { Host = "127.0.0.1", Port = port, Transport = Transport.Udp } },
                    new MultiResolverOptions { DnsSecTrustAnchors = new[] { anchor }, RequestDnsSec = true, ValidateDnsSec = true });
                result = await resolver.QueryAsync("www.example.com", DnsRecordType.A, deadline.Token);
            } else result = await client.Resolve("www.example.com", DnsRecordType.A, requestDnsSec: true, validateDnsSec: true, cancellationToken: deadline.Token);
            Assert.True(result.DnsSecValidationStatus == DnsSecValidationStatus.Secure, result.DnsSecValidationMessage);
            Assert.Equal("192.0.2.9", result.Answers!.Single(record => record.Type == DnsRecordType.A).Data);
            Assert.True(result.DnsSecValidationExpiresUtc <= anchor.Anchor.ValidUntil);
            Assert.InRange(result.Answers.Single(record => record.Type == DnsRecordType.A).TTL, 0, 30);
            Assert.Contains(observed, query => query.Type == DnsRecordType.DNSKEY && query.Name == fixture.Zone.Name);
            Assert.DoesNotContain(observed, query => query.Type == DnsRecordType.DS || query.Name == ".");
            if (entryPoint == 0) {
                DnsResponse cached = await client.Resolve("www.example.com", DnsRecordType.A, requestDnsSec: true, validateDnsSec: true, cancellationToken: deadline.Token);
                Assert.True(cached.ServedFromCache);
                client.EndpointConfiguration.DnsSecTrustAnchors = new[] { Anchor(fixture, until: fixture.Now.AddSeconds(-1)) };
                DnsResponse rejected = await client.Resolve("www.example.com", DnsRecordType.A, requestDnsSec: true, validateDnsSec: true, cancellationToken: deadline.Token);
                Assert.Equal(DnsSecValidationStatus.Bogus, rejected.DnsSecValidationStatus);
                Assert.False(rejected.ServedFromCache);
            }
        } finally { deadline.Cancel(); await server; }

        async Task Serve() {
            try {
                while (!deadline.IsCancellationRequested) {
                    UdpReceiveResult incoming = await udp.ReceiveAsync();
                    var reader = new DnsWireReader(incoming.Buffer, 12);
                    string name = DnsWireNameCodec.Canonical(reader.ReadName());
                    var type = (DnsRecordType)reader.ReadUInt16(); reader.ReadUInt16();
                    observed.Add((name, type));
                    Assert.True(type == DnsRecordType.DNSKEY && name == fixture.Zone.Name || type == DnsRecordType.A && name == "www.example.com.", $"Unexpected signed-zone query: {name} {type}");
                    byte[] bytes = DnsSecSignedFixture.SerializeResponse(incoming.Buffer, reader.Position, type == DnsRecordType.DNSKEY ? keys : answer);
                    await udp.SendAsync(bytes, bytes.Length, incoming.RemoteEndPoint);
                }
            } catch (Exception exception) when (deadline.IsCancellationRequested && exception is SocketException or ObjectDisposedException) { }
        }
    }

    private static DnsSecZoneTrustAnchor Anchor(DnsSecSignedFixture fixture, DateTimeOffset? until = null) {
        Assert.True(DnsSecCrypto.TryComputeDsDigest(fixture.Zone.Name, fixture.Zone, 2, out byte[] digest));
        return new DnsSecZoneTrustAnchor(fixture.Zone.Name, new DnsSecTrustAnchor(fixture.Zone.KeyTag, (DnsKeyAlgorithm)fixture.Zone.Algorithm, 2,
            BitConverter.ToString(digest).Replace("-", string.Empty), validUntil: until));
    }

    private static DnsSecValidationEngine Engine(DnsSecSignedFixture fixture, IReadOnlyList<DnsSecZoneTrustAnchor> anchors,
        Func<string, DnsRecordType, DnsResponse?>? extra = null) => new((name, type, token) => {
            DnsResponse? reply = extra?.Invoke(name, type);
            if (reply != null) return Task.FromResult(reply);
            Assert.True(type == DnsRecordType.DNSKEY && name == fixture.Zone.Name, $"A private-zone lookup escaped its trust boundary: {name} {type}");
            return fixture.LookupAsync(name, type, token);
        }, fixture.Now, zoneTrustAnchors: anchors);

    private static string CacheKey(Configuration configuration) => DnsCacheKeyBuilder.Build(configuration, "www.example.com", DnsRecordType.A,
        true, true, false, false, false, TimeSpan.FromHours(1), false);
}
