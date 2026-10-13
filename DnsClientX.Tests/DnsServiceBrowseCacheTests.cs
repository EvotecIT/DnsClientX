using System;
using System.Linq;
using System.Threading.Tasks;

namespace DnsClientX.Tests;
/// <summary>Protects mDNS cache-flush, rescue, expiry, refresh, known-answer, and bounded-state contracts.</summary>
public class DnsServiceBrowseCacheTests {
    private const string ServiceType = "_http._tcp.local";
    private const string Instance = "site._http._tcp.local";
    /// <summary>TXT/SRV changes are updates; a rescued goodbye survives, and a final goodbye removes the last live snapshot.</summary>
    [Fact]
    public async Task UpdatesAndGoodbyeRescueRespectOneSecondGrace() {
        var cache = new DnsServiceBrowseCache(ServiceType, 32, 8);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Ptr(ServiceType, Instance), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80), DnsDiscoveryTestWire.Txt(Instance, "path=/old")), 10);
        Assert.Equal(DnsServiceBrowseChange.Added, Assert.Single(cache.Advance(10, DateTimeOffset.UtcNow)).Change);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Txt(Instance, "path=/new")), 12);
        DnsServiceBrowseEvent updated = Assert.Single(cache.Advance(12, DateTimeOffset.UtcNow));
        Assert.Equal(DnsServiceBrowseChange.Updated, updated.Change);
        Assert.Equal("/new", updated.Service.Metadata!["path"]);
        Assert.Empty(cache.Advance(13.1, DateTimeOffset.UtcNow));
        Assert.Single(cache.Entries, entry => entry.Record.Type == DnsRecordType.TXT);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Ptr(ServiceType, Instance, ttl: 0)), 14);
        Assert.Empty(cache.Advance(14.99, DateTimeOffset.UtcNow));
        cache.Apply(await Packet(DnsDiscoveryTestWire.Ptr(ServiceType, Instance)), 14.995);
        Assert.Empty(cache.Advance(15.1, DateTimeOffset.UtcNow));
        cache.Apply(await Packet(DnsDiscoveryTestWire.Srv(Instance, "newhost.local", 443)), 16);
        DnsServiceBrowseEvent endpoint = Assert.Single(cache.Advance(16, DateTimeOffset.UtcNow));
        Assert.Equal(DnsServiceBrowseChange.Updated, endpoint.Change);
        Assert.Equal("newhost.local", endpoint.Service.Target);
        Assert.Equal(443, endpoint.Service.Port);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Ptr(ServiceType, Instance, ttl: 0)), 17);
        DnsServiceBrowseEvent removed = Assert.Single(cache.Advance(18.01, DateTimeOffset.UtcNow));
        Assert.Equal(DnsServiceBrowseChange.Removed, removed.Change);
        Assert.Equal("newhost.local", removed.Service.Target);
    }

    /// <summary>Cache flush leaves recent RRset burst members alive and expires older members after one second.</summary>
    [Fact]
    public async Task CacheFlushPreservesRecentBurstMembers() {
        var cache = new DnsServiceBrowseCache(ServiceType, 32, 8);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Ptr(ServiceType, Instance), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80), DnsDiscoveryTestWire.Address("host.local", "192.0.2.1")), 10);
        cache.Advance(10, DateTimeOffset.UtcNow);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Address("host.local", "192.0.2.2")), 12);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Address("host.local", "192.0.2.3")), 12.1);
        cache.Advance(12.9, DateTimeOffset.UtcNow);
        Assert.Equal(3, cache.Entries.Count(entry => entry.Record.Type == DnsRecordType.A));
        DnsServiceBrowseEvent update = Assert.Single(cache.Advance(13.2, DateTimeOffset.UtcNow));
        Assert.Equal(DnsServiceBrowseChange.Updated, update.Change);
        Assert.Equal(new[] { "192.0.2.2", "192.0.2.3" }, update.Addresses.Select(address => address.ToString()));
    }

    /// <summary>TTL expiry removes the instance, while refresh questions and known answers retain their separate lifetime rules.</summary>
    [Fact]
    public async Task RefreshAndKnownAnswersFollowRecordLifetime() {
        var cache = new DnsServiceBrowseCache(ServiceType, 32, 8);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Ptr(ServiceType, Instance, ttl: 100), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80, ttl: 100)), 0);
        cache.Advance(0, DateTimeOffset.UtcNow);
        Assert.Empty(cache.RefreshQuestions(79).ToArray());
        Assert.Equal(2, cache.RefreshQuestions(82.01).Count());
        Assert.Empty(cache.RefreshQuestions(83).ToArray());
        Assert.Equal(2, cache.RefreshQuestions(87.01).Count());
        DnsAnswer known = Assert.Single(cache.KnownAnswers(ServiceType, DnsRecordType.PTR, 50));
        Assert.Equal(50, known.TTL);
        Assert.Empty(cache.KnownAnswers(ServiceType, DnsRecordType.PTR, 50.01));
        byte[] query = DnsWireResolveMulticast.CreateBrowseQuery(ServiceType, DnsRecordType.PTR, new[] { known });
        DnsResponse parsed = await DnsWire.DeserializeDnsWireFormat(null, bytes: query);
        Assert.Single(parsed.Answers);
        Assert.Equal((ushort)1, parsed.Answers[0].Class);
        Assert.Equal(50, parsed.Answers[0].TTL);
        Assert.Equal(Instance, parsed.Answers[0].Data);
        Assert.Equal(DnsServiceBrowseChange.Expired, Assert.Single(cache.Advance(100.01, DateTimeOffset.UtcNow)).Change);
    }

    /// <summary>Unrelated announcements do not consume cache space, but exceeding a real configured bound fails explicitly.</summary>
    [Fact]
    public async Task OnlyRelatedRecordsConsumeExplicitBounds() {
        var cache = new DnsServiceBrowseCache(ServiceType, 2, 1);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Ptr(ServiceType, Instance), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80), DnsDiscoveryTestWire.Address("unrelated.local", "192.0.2.99")), 0);
        Assert.Equal(2, cache.Entries.Count());
        Assert.Single(cache.Advance(0, DateTimeOffset.UtcNow));
        DnsResponse more = await Packet(DnsDiscoveryTestWire.Txt(Instance, "path=/"));
        Assert.Throws<DnsClientException>(() => cache.Apply(more, 1));
    }

    /// <summary>Consumers cannot mutate internal service snapshots and thereby hide subsequent updates or corrupt removals.</summary>
    [Fact]
    public async Task ReturnedSnapshotsDoNotMutateCacheState() {
        var cache = new DnsServiceBrowseCache(ServiceType, 32, 8);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Ptr(ServiceType, Instance, ttl: 1), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80), DnsDiscoveryTestWire.Txt(Instance, "path=/original")), 0);
        DnsServiceBrowseEvent added = Assert.Single(cache.Advance(0, DateTimeOffset.UtcNow));
        added.Service.Target = "attacker.local";
        added.Service.Metadata!["path"] = "/changed";
        DnsServiceBrowseEvent expired = Assert.Single(cache.Advance(1.01, DateTimeOffset.UtcNow));
        Assert.Equal("host.local", expired.Service.Target);
        Assert.Equal("/original", expired.Service.Metadata!["path"]);
    }

    /// <summary>Link-local IPv6 target addresses retain the selected browsing interface so callers can connect to them.</summary>
    [Fact]
    public async Task ScopedBrowseReturnsUsableLinkLocalIpv6Addresses() {
        var cache = new DnsServiceBrowseCache(ServiceType, 32, 8, interfaceIndex: 17);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Ptr(ServiceType, Instance), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80),
            DnsDiscoveryTestWire.Address("host.local", "fe80::10")), 0);
        DnsServiceBrowseEvent added = Assert.Single(cache.Advance(0, DateTimeOffset.UtcNow));
        Assert.Equal(17, Assert.Single(added.Addresses).ScopeId);
    }

    /// <summary>The continuous projection preserves friendly PTR instance casing and matches the snapshot service-type presentation.</summary>
    [Fact]
    public async Task BrowseProjectionPreservesInstancePresentation() {
        var cache = new DnsServiceBrowseCache(ServiceType, 32, 8);
        cache.Apply(await Packet(DnsDiscoveryTestWire.Ptr(ServiceType, "OfficePrinter._http._tcp.local"),
            DnsDiscoveryTestWire.Srv("OfficePrinter._http._tcp.local", "host.local", 80)), 0);
        DnsServiceBrowseEvent added = Assert.Single(cache.Advance(0, DateTimeOffset.UtcNow));
        Assert.Equal("OfficePrinter._http._tcp.local", added.Service.ServiceName);
        Assert.Equal(ServiceType, added.Service.ServiceType);
    }

    private static Task<DnsResponse> Packet(params DnsDiscoveryTestWire.Record[] records) => DnsWire.DeserializeDnsMulticastResponse(DnsDiscoveryTestWire.Response(null, records));
}
