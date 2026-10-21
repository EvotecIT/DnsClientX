#if NET8_0_OR_GREATER
using System.Text.Json;

namespace DnsClientX.Tests;

internal sealed class BindAuthoritativeTheoryAttribute : TheoryAttribute {
    public BindAuthoritativeTheoryAttribute() {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DNSCLIENTX_BIND_MANIFEST"))) {
            Skip = "Run scripts/qualification/qualify-bind.py to create the independent BIND lab.";
        }
    }
}

/// <summary>Qualifies public client operations against ephemeral zones produced and served by BIND.</summary>
[Collection("NoParallel")]
public sealed class BindAuthoritativeTests {
    private static JsonElement Manifest() => JsonDocument.Parse(File.ReadAllText(
        Environment.GetEnvironmentVariable("DNSCLIENTX_BIND_MANIFEST")!)).RootElement.Clone();

    private static ClientX Client(string address = "127.0.0.1", bool authenticated = false) {
        JsonElement manifest = Manifest();
        var configuration = new Configuration(address, DnsRequestFormat.DnsOverTCP) {
            Port = manifest.GetProperty("port").GetInt32(), TimeOut = 5000,
            EnableQNameMinimization = false
        };
        if (authenticated) {
            configuration.TsigKey = TsigKey.FromBase64("qualification-key", manifest.GetProperty("secret").GetString()!);
        }
        return new ClientX(configuration, enableCache: false);
    }

    /// <summary>Private DS anchors authenticate independent RSA signatures, aliases and authenticated denial over IPv4/IPv6.</summary>
    [BindAuthoritativeTheory]
    [InlineData(0, "127.0.0.1")]
    [InlineData(1, "127.0.0.1")]
    [InlineData(0, "::1")]
    [InlineData(1, "::1")]
    public async Task PrivateAnchorAuthenticatesSignedAliasesAndDenial(int zoneIndex, string address) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        JsonElement zone = Manifest().GetProperty("zones")[zoneIndex];
        string apex = zone.GetProperty("name").GetString()!;
        using ClientX client = Client(address);
        client.EndpointConfiguration.DnsSecTrustAnchors = new[] { Anchor(zone) };
        foreach (var query in new[] {
            ("www.", DnsRecordType.A, DnsResponseCode.NoError, "192.0.2.9"),
            ("www.", DnsRecordType.AAAA, DnsResponseCode.NoError, "2001:db8::9"),
            ("alias.", DnsRecordType.A, DnsResponseCode.NoError, "192.0.2.9"),
            ("host.old.", DnsRecordType.A, DnsResponseCode.NoError, "192.0.2.10"),
            ("absent.", DnsRecordType.A, DnsResponseCode.NXDomain, ""),
            ("host.new.", DnsRecordType.AAAA, DnsResponseCode.NoError, "")
        }) {
            DnsResponse answer = await client.Resolve(query.Item1 + apex, query.Item2,
                requestDnsSec: true, validateDnsSec: true, retryOnTransient: false, cancellationToken: deadline.Token);
            Assert.True(query.Item3 == answer.Status,
                $"{query.Item1}{apex} {query.Item2} via {address}: expected {query.Item3}, got {answer.Status}; " +
                $"{answer.ErrorCode}: {answer.Error}; {answer.DnsSecValidationMessage}; {answer.Exception}");
            Assert.True(answer.DnsSecValidationStatus == DnsSecValidationStatus.Secure,
                query.Item1 + apex + ": " + answer.DnsSecValidationMessage);
            Assert.NotNull(answer.DnsSecValidationExpiresUtc);
            if (query.Item4.Length > 0) {
                Assert.Contains(answer.Answers, record => record.Type == query.Item2 && record.Data == query.Item4);
            } else {
                Assert.DoesNotContain(answer.Answers, record => record.Type == query.Item2);
            }
        }
        // A valid server signature cannot recover from the caller's wrong or expired private boundary.
        foreach (var anchor in new[] { Anchor(zone, wrongDigest: true), Anchor(zone, expired: true) }) {
            client.EndpointConfiguration.DnsSecTrustAnchors = new[] { anchor };
            DnsResponse rejected = await client.Resolve("www." + apex, DnsRecordType.A,
                requestDnsSec: true, validateDnsSec: true, retryOnTransient: false, cancellationToken: deadline.Token);
            Assert.Equal(DnsSecValidationStatus.Bogus, rejected.DnsSecValidationStatus);
        }
    }

    /// <summary>BIND applies complete RRset prerequisites atomically and authenticates multi-frame AXFR and journal-backed IXFR.</summary>
    [BindAuthoritativeTheory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task AtomicUpdatesAndAuthenticatedTransfers(string address) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        const string zone = "updates.qualification.test";
        string marker = "batch" + Guid.NewGuid().ToString("N");
        string first = marker + "." + zone;
        string second = "second." + first;
        using ClientX client = Client(address, authenticated: true);
        ZoneTransferResult[] baseline = await client.ZoneTransferAsync(zone, retryOnTransient: false, cancellationToken: deadline.Token);
        uint initialSerial = Assert.IsType<SoaRecord>(DnsRecordFactory.Create(baseline.SelectMany(frame => frame.Records)
            .First(record => record.Type == DnsRecordType.SOA))).Serial;
        var additions = new[] {
            DnsUpdateOperation.Add(first, DnsRecordType.A, "192.0.2.30"),
            DnsUpdateOperation.Add(second, DnsRecordType.A, "192.0.2.31")
        };
        await client.UpdateRecordsAsync(new DnsUpdateBatch(zone, additions, new[] {
            DnsUpdatePrerequisite.NameAbsent(first),
            DnsUpdatePrerequisite.RrsetEquals("existing." + zone, DnsRecordType.A, new[] { "192.0.2.2", "192.0.2.1" })
        }), deadline.Token);
        await Assert.ThrowsAsync<DnsClientException>(() => client.UpdateRecordsAsync(new DnsUpdateBatch(zone,
            new[] { DnsUpdateOperation.DeleteName(first), DnsUpdateOperation.DeleteName(second) }, new[] {
                DnsUpdatePrerequisite.RrsetEquals("existing." + zone, DnsRecordType.A, new[] { "192.0.2.1" })
            }), deadline.Token));
        // Neither deletion from the rejected batch may have been applied.
        foreach (string name in new[] { first, second }) {
            DnsResponse response = await client.Resolve(name, DnsRecordType.A, retryOnTransient: false, cancellationToken: deadline.Token);
            Assert.Single(response.Answers, record => record.Type == DnsRecordType.A);
        }
        IncrementalZoneTransferResult changes = await client.IncrementalZoneTransferAsync(zone, initialSerial, deadline.Token);
        Assert.Equal(IncrementalZoneTransferKind.Incremental, changes.Kind);
        Assert.Contains(changes.Changes.SelectMany(change => change.AddedRecords), record => record.Name.TrimEnd('.') == first);
        ZoneTransferResult[] transferred = await client.ZoneTransferAsync(zone, retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.True(transferred.Length > 2, "BIND one-answer transfer must exercise multiple authenticated frames.");
        Assert.Equal(2, transferred.SelectMany(frame => frame.Records).Count(record => record.Type == DnsRecordType.SOA));
        Assert.Contains(transferred.SelectMany(frame => frame.Records), record => record.Name.TrimEnd('.') == second);
        Assert.Equal(IncrementalZoneTransferKind.NoChange,
            (await client.IncrementalZoneTransferAsync(zone, changes.CurrentSoa, deadline.Token)).Kind);

        using ClientX wrongKey = Client(address, authenticated: true);
        wrongKey.EndpointConfiguration.TsigKey = new TsigKey("qualification-key", new byte[32]);
        await Assert.ThrowsAsync<DnsClientException>(() => wrongKey.ZoneTransferAsync(zone, retryOnTransient: false, cancellationToken: deadline.Token));
        await client.UpdateRecordsAsync(new DnsUpdateBatch(zone, new[] {
            DnsUpdateOperation.DeleteName(first), DnsUpdateOperation.DeleteName(second)
        }, new[] { DnsUpdatePrerequisite.NameExists(first), DnsUpdatePrerequisite.RrsetExists(second, DnsRecordType.A) }), deadline.Token);
        DnsResponse removed = await client.Resolve(first, DnsRecordType.A, retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.Equal(DnsResponseCode.NXDomain, removed.Status);
    }

    private static DnsSecZoneTrustAnchor Anchor(JsonElement zone, bool wrongDigest = false, bool expired = false) => new(
        zone.GetProperty("name").GetString()!, new DnsSecTrustAnchor(zone.GetProperty("keyTag").GetUInt16(),
            (DnsKeyAlgorithm)zone.GetProperty("algorithm").GetByte(), zone.GetProperty("digestType").GetByte(),
            wrongDigest ? new string('0', 64) : zone.GetProperty("digest").GetString()!,
            validUntil: expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : null));
}
#endif
