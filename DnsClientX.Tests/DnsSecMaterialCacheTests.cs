using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Protects authenticated material reuse against cache poisoning and lifetime renewal.</summary>
public class DnsSecMaterialCacheTests {
    /// <summary>Independent signed answers reuse the chain without another DNSKEY or DS fetch.</summary>
    [Fact]
    public async Task AcceptedMaterialIsReusedAndClearedWithoutRenewingItsReceipt() {
        using var fixture = new DnsSecSignedFixture();
        using var cache = new DnsResponseCache();
        int lookups = 0;
        async Task<DnsResponse> Lookup(string name, DnsRecordType type, CancellationToken token) {
            Interlocked.Increment(ref lookups);
            return await fixture.LookupAsync(name, type, token);
        }
        async Task<DnsSecValidationDependency[]> Validate(string name) {
            var session = new DnsSecMaterialSession(cache, (owner, type) => owner + ":" + type, Lookup);
            var validator = new DnsSecValidationEngine(session.LookupAsync, trustAnchorStorePath: fixture.AnchorPath);
            var answer = fixture.Signed(name, DnsRecordType.A, new byte[] { 192, 0, 2, 1 });
            var result = await validator.ValidateAsync(answer, name, DnsRecordType.A, default);
            Assert.Equal(DnsSecValidationStatus.Secure, result.Status);
            return session.Complete(validator, result.Status, TimeSpan.FromMinutes(5));
        }
        var first = await Validate("one.example.com");
        int firstLookups = lookups;
        Assert.True(firstLookups >= 3);
        Assert.All(first, item => Assert.NotNull(item.ReusableUntilUtc));
        var second = await Validate("two.example.com");
        Assert.Equal(firstLookups, lookups);
        Assert.All(second, item => Assert.Equal(DnsResponseSource.Cache, item.ResponseSource));
        Assert.True(cache.Clear() >= 3);
        await Validate("three.example.com");
        Assert.Equal(firstLookups * 2, lookups);
    }

    /// <summary>A bogus answer cannot authorize caching the otherwise well-formed chain material.</summary>
    [Fact]
    public async Task BogusValidationDoesNotPopulateMaterialCache() {
        using var fixture = new DnsSecSignedFixture();
        using var cache = new DnsResponseCache();
        for (int index = 0; index < 2; index++) {
            var session = new DnsSecMaterialSession(cache, (name, type) => name + ":" + type,
                (name, type, token) => fixture.LookupAsync(name, type, token));
            var validator = new DnsSecValidationEngine(session.LookupAsync, trustAnchorStorePath: fixture.AnchorPath);
            var answer = fixture.Signed("www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 });
            answer.WireMessage[0] ^= 1;
            var result = await validator.ValidateAsync(answer, "www.example.com", DnsRecordType.A, default);
            Assert.Equal(DnsSecValidationStatus.Bogus, result.Status);
            var dependencies = session.Complete(validator, result.Status, TimeSpan.FromMinutes(5));
            Assert.All(dependencies, item => Assert.Null(item.ReusableUntilUtc));
            Assert.Equal(0, cache.GetStatistics().EntryCount);
        }
    }

    /// <summary>Reusing material never resets its receipt-based authenticated TTL.</summary>
    [Fact]
    public async Task ExpiredMaterialRequiresAnotherFetch() {
        using var fixture = new DnsSecSignedFixture { MaterialTtl = 2 };
        using var cache = new DnsResponseCache();
        int lookups = 0;
        async Task Validate() {
            var session = new DnsSecMaterialSession(cache, (name, type) => name + ":" + type, async (name, type, token) => {
                lookups++;
                return await fixture.LookupAsync(name, type, token);
            });
            var validator = new DnsSecValidationEngine(session.LookupAsync, trustAnchorStorePath: fixture.AnchorPath);
            var result = await validator.ValidateAsync(fixture.Signed("www.example.com", DnsRecordType.A,
                new byte[] { 192, 0, 2, 1 }), "www.example.com", DnsRecordType.A, default);
            Assert.Equal(DnsSecValidationStatus.Secure, result.Status);
            session.Complete(validator, result.Status, TimeSpan.FromMinutes(5));
        }
        await Validate();
        int first = lookups;
        await Validate();
        Assert.Equal(first, lookups);
        await Task.Delay(2200);
        await Validate();
        Assert.Equal(first * 2, lookups);
    }
}
