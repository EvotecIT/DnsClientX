using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests covering DNS server hostname resolution behavior.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsServerResolverTests : IDisposable {
        /// <summary>
        /// Initializes a new instance of the <see cref="DnsServerResolverTests"/> class.
        /// </summary>
        public DnsServerResolverTests() {
            DnsServerResolver.ResetForTests();
        }

        /// <summary>
        /// Resets shared resolver state after each test.
        /// </summary>
        public void Dispose() {
            DnsServerResolver.ResetForTests();
        }

        /// <summary>Stale reuse reports its lookup failure without changing a successful query into SERVFAIL.</summary>
        [Fact]
        public async Task StaleLookupRetainsDiagnosticProvenance() {
            var configuration = new Configuration("diagnostic.local", DnsRequestFormat.DnsOverUDP) { DnsServerResolutionSuccessTtl = TimeSpan.Zero };
            DnsServerResolver.ResolveHostAddressesAsync = _ => Task.FromResult(new[] { IPAddress.Loopback });
            await DnsServerResolver.ResolveAsync("diagnostic.local", configuration, default);
            DnsServerResolver.ResolveHostAddressesAsync = _ => throw new InvalidOperationException("lookup unavailable");
            var stale = await DnsServerResolver.ResolveAsync("diagnostic.local", configuration, default);
            Assert.Equal(IPAddress.Loopback, stale.Address);
            Assert.Null(stale.Error);
            Assert.True(configuration.ServerResolution!.UsedStaleAddress);
            Assert.Equal("lookup unavailable", configuration.ServerResolution.Error);
            Assert.Null(configuration.ServerResolution.BootstrapResolver);
        }

        /// <summary>A system client can deliberately refresh servers and policy after network changes.</summary>
        [Fact]
        public void SystemConfigurationRefreshesOperatingSystemSnapshot() {
            var servers = new System.Collections.Generic.List<string> { "192.0.2.1" };
            SystemInformation.SetDnsServerProvider(() => servers);
            try {
                var configuration = new Configuration(DnsEndpoint.System);
                Assert.Equal("192.0.2.1", configuration.Hostname);
                servers = new System.Collections.Generic.List<string> { "192.0.2.2" };
                configuration.RefreshSystemDns();
                Assert.Equal("192.0.2.2", configuration.Hostname);
                Assert.Equal("192.0.2.2", Assert.Single(configuration.SystemDnsConfiguration!.DnsServers));
                Assert.Throws<InvalidOperationException>(() => new Configuration(DnsEndpoint.Cloudflare).RefreshSystemDns());
            } finally { SystemInformation.SetDnsServerProvider(null); }
        }

        /// <summary>
        /// Ensures IP literals are returned without resolution errors.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_WithIp_ReturnsAddress() {
            var (address, error) = await DnsServerResolver.ResolveAsync(
                "8.8.8.8",
                1000,
                CancellationToken.None);

            Assert.NotNull(address);
            Assert.Null(error);
            Assert.Equal(IPAddress.Parse("8.8.8.8"), address);
        }

        /// <summary>Stale reuse must not escape its caller policy or extend its original expiry.</summary>
        [Fact]
        public async Task StaleResultsRespectPolicyAndOriginalLifetime() {
            DnsServerResolver.ResolveHostAddressesAsync = _ => Task.FromResult(new[] { IPAddress.Loopback });
            await DnsServerResolver.ResolveAsync("stale-policy.local", 1000, CancellationToken.None,
                successTtl: TimeSpan.Zero, failureTtl: TimeSpan.FromSeconds(10), staleTtl: TimeSpan.FromMilliseconds(150));
            DnsServerResolver.ResolveHostAddressesAsync = _ => throw new InvalidOperationException("resolver failure");
            var allowed = await DnsServerResolver.ResolveAsync("stale-policy.local", 1000, CancellationToken.None,
                successTtl: TimeSpan.Zero, failureTtl: TimeSpan.FromSeconds(10), staleTtl: TimeSpan.FromMilliseconds(150));
            var forbidden = await DnsServerResolver.ResolveAsync("stale-policy.local", 1000, CancellationToken.None,
                successTtl: TimeSpan.Zero, failureTtl: TimeSpan.FromSeconds(10), allowStale: false, staleTtl: TimeSpan.FromMilliseconds(150));
            Assert.Equal(IPAddress.Loopback, allowed.Address);
            Assert.Null(allowed.Error);
            Assert.Null(forbidden.Address);
            Assert.NotNull(forbidden.Error);
            await Task.Delay(200);
            var expired = await DnsServerResolver.ResolveAsync("stale-policy.local", 1000, CancellationToken.None,
                successTtl: TimeSpan.Zero, failureTtl: TimeSpan.FromSeconds(10), staleTtl: TimeSpan.FromMilliseconds(150));
            Assert.Null(expired.Address);
            Assert.NotNull(expired.Error);
        }

        /// <summary>Different TTL policies cannot reuse a long-lived entry or in-flight lookup.</summary>
        [Fact]
        public async Task HostnameCacheSeparatesTtlPolicies() {
            int calls = 0;
            DnsServerResolver.ResolveHostAddressesAsync = _ => {
                Interlocked.Increment(ref calls);
                return Task.FromResult(new[] { IPAddress.Loopback });
            };
            await DnsServerResolver.ResolveAsync("ttl-policy.local", 1000, CancellationToken.None, successTtl: TimeSpan.FromMinutes(1));
            await DnsServerResolver.ResolveAsync("ttl-policy.local", 1000, CancellationToken.None, successTtl: TimeSpan.Zero);
            await DnsServerResolver.ResolveAsync("ttl-policy.local", 1000, CancellationToken.None, successTtl: TimeSpan.Zero);
            Assert.Equal(3, calls);
        }

        /// <summary>
        /// Ensures empty hostnames yield a validation error.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_WithEmpty_ReturnsError() {
            var (address, error) = await DnsServerResolver.ResolveAsync(
                " ",
                1000,
                CancellationToken.None);

            Assert.Null(address);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        /// <summary>
        /// Ensures a custom resolver delegate is honored.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_UsesCustomResolver() {
            DnsServerResolver.ResolveHostAddressesAsync = _ => Task.FromResult(new[] { IPAddress.Loopback });

            var (address, error) = await DnsServerResolver.ResolveAsync(
                "custom.local",
                1000,
                CancellationToken.None);

            Assert.NotNull(address);
            Assert.Null(error);
            Assert.Equal(IPAddress.Loopback, address);
        }

        /// <summary>
        /// Ensures address-family preference is honored and cached independently by family.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_HonorsPreferredAddressFamily() {
            int calls = 0;
            DnsServerResolver.ResolveHostAddressesAsync = _ => {
                calls++;
                return Task.FromResult(new[] { IPAddress.Parse("192.0.2.1"), IPAddress.Parse("2001:db8::1") });
            };

            var ipv6 = await DnsServerResolver.ResolveAsync(
                "dual-stack.local", 1000, CancellationToken.None,
                preferredAddressFamily: System.Net.Sockets.AddressFamily.InterNetworkV6);
            var ipv4 = await DnsServerResolver.ResolveAsync(
                "dual-stack.local", 1000, CancellationToken.None,
                preferredAddressFamily: System.Net.Sockets.AddressFamily.InterNetwork);

            Assert.Equal(IPAddress.Parse("2001:db8::1"), ipv6.Address);
            Assert.Equal(IPAddress.Parse("192.0.2.1"), ipv4.Address);
            Assert.Equal(2, calls);
        }

        /// <summary>
        /// Ensures stale cached addresses can be reused when resolution fails.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_UsesStaleAddressOnFailure() {
            var callCount = 0;
            DnsServerResolver.ResolveHostAddressesAsync = _ => {
                callCount++;
                if (callCount == 1) {
                    return Task.FromResult(new[] { IPAddress.Loopback });
                }
                throw new InvalidOperationException("resolver failure");
            };

            var first = await DnsServerResolver.ResolveAsync(
                "stale.local",
                1000,
                CancellationToken.None,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                true,
                TimeSpan.FromMinutes(1));

            Assert.NotNull(first.Address);
            Assert.Equal(IPAddress.Loopback, first.Address);

            var second = await DnsServerResolver.ResolveAsync(
                "stale.local",
                1000,
                CancellationToken.None,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                true,
                TimeSpan.FromMinutes(1));

            Assert.NotNull(second.Address);
            Assert.Equal(IPAddress.Loopback, second.Address);
        }

        /// <summary>
        /// Ensures concurrent resolutions for the same hostname are deduplicated.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_DeduplicatesConcurrentResolution() {
            var callCount = 0;
            var tcs = new TaskCompletionSource<IPAddress[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            DnsServerResolver.ResolveHostAddressesAsync = _ => {
                Interlocked.Increment(ref callCount);
                return tcs.Task;
            };

            Task<(IPAddress? Address, string? Error)> firstTask = DnsServerResolver.ResolveAsync(
                "concurrent.local",
                1000,
                CancellationToken.None);

            Task<(IPAddress? Address, string? Error)> secondTask = DnsServerResolver.ResolveAsync(
                "concurrent.local",
                1000,
                CancellationToken.None);

            SpinWait.SpinUntil(() => Volatile.Read(ref callCount) > 0, 1000);
            Assert.Equal(1, Volatile.Read(ref callCount));

            tcs.SetResult([IPAddress.Loopback]);
            var results = await Task.WhenAll(firstTask, secondTask);

            Assert.All(results, result => Assert.Equal(IPAddress.Loopback, result.Address));
        }

        /// <summary>
        /// Ensures cache trimming evicts least-recently-used entries.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_TrimsLeastRecentlyUsedEntries() {
            DnsServerResolver.MaxEntries = 1;
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            DnsServerResolver.ResolveHostAddressesAsync = host => {
                counts[host] = counts.TryGetValue(host, out var current) ? current + 1 : 1;
                return Task.FromResult(new[] { IPAddress.Loopback });
            };

            await DnsServerResolver.ResolveAsync(
                "host-a.local",
                1000,
                CancellationToken.None,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromSeconds(10),
                true,
                TimeSpan.FromMinutes(1));

            await Task.Delay(10);

            await DnsServerResolver.ResolveAsync(
                "host-b.local",
                1000,
                CancellationToken.None,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromSeconds(10),
                true,
                TimeSpan.FromMinutes(1));

            await DnsServerResolver.ResolveAsync(
                "host-a.local",
                1000,
                CancellationToken.None,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromSeconds(10),
                true,
                TimeSpan.FromMinutes(1));

            Assert.Equal(2, counts["host-a.local"]);
        }
    }
}
