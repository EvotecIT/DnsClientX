using System;
using System.Net;
using System.Net.Sockets;
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

        /// <summary>Lookup deadlines retain their category and cause when the cached failure is reused.</summary>
        [Theory]
        [InlineData(DnsRequestFormat.DnsOverUDP)]
        [InlineData(DnsRequestFormat.DnsOverTCP)]
        [InlineData(DnsRequestFormat.DnsOverTLS)]
        public async Task CachedHostnameDeadlineRetainsTransportDiagnostics(DnsRequestFormat format) {
            int calls = 0;
            var pending = new TaskCompletionSource<IPAddress[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            DnsServerResolver.ResolveHostAddressesAsync = _ => { calls++; return pending.Task; };
            var configuration = new Configuration("deadline.example", format) {
                Hostname = "deadline.example", TimeOut = 25, DnsServerResolutionAllowStale = false
            };
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try {
                var first = await QueryHostname(configuration, deadline.Token);
                var cached = await QueryHostname(configuration, deadline.Token);
                Assert.Equal(DnsQueryErrorCode.Timeout, first.ErrorCode);
                Assert.IsType<TimeoutException>(first.Exception);
                Assert.Equal(DnsQueryErrorCode.Timeout, cached.ErrorCode);
                Assert.Same(first.Exception, cached.Exception);
                Assert.True(cached.ServerResolution!.ServedFromCache);
                Assert.Equal(first.ServerResolution!.Error, cached.ServerResolution.Error);
                Assert.Equal(1, calls);
            } finally { pending.TrySetResult([IPAddress.Loopback]); }
        }

        /// <summary>Resolver-host network failures retain their original exception across cached queries.</summary>
        [Theory]
        [InlineData(DnsRequestFormat.DnsOverUDP)]
        [InlineData(DnsRequestFormat.DnsOverTCP)]
        [InlineData(DnsRequestFormat.DnsOverTLS)]
        public async Task CachedHostnameFailureRetainsTransportDiagnostics(DnsRequestFormat format) {
            int calls = 0;
            var failure = new SocketException((int)SocketError.HostNotFound);
            DnsServerResolver.ResolveHostAddressesAsync = _ => { calls++; throw failure; };
            var configuration = new Configuration("missing.example", format) {
                Hostname = "missing.example", TimeOut = 1000, DnsServerResolutionAllowStale = false
            };
            var first = await QueryHostname(configuration, default);
            var cached = await QueryHostname(configuration, default);
            Assert.Equal(DnsQueryErrorCode.Network, first.ErrorCode);
            Assert.Same(failure, first.Exception);
            Assert.Equal(DnsQueryErrorCode.Network, cached.ErrorCode);
            Assert.Same(failure, cached.Exception);
            Assert.True(cached.ServerResolution!.ServedFromCache);
            Assert.Equal(failure.Message, cached.ServerResolution.Error);
            Assert.Equal(1, calls);
        }

        private static async Task<DnsResponse> QueryHostname(Configuration configuration, CancellationToken token) {
            if (configuration.RequestFormat == DnsRequestFormat.DnsOverUDP) {
                return await DnsWireResolveUdp.ResolveWireFormatUdp(configuration.Hostname!, 53,
                    "payload.example", DnsRecordType.A, false, false, false, configuration, 1, token);
            }
            if (configuration.RequestFormat == DnsRequestFormat.DnsOverTCP) {
                return await DnsWireResolveTcp.ResolveWireFormatTcp(configuration.Hostname!, 53,
                    "payload.example", DnsRecordType.A, false, false, false, configuration, token);
            }
            var failure = await Assert.ThrowsAsync<DnsClientException>(() => DnsWireResolveDot.ResolveWireFormatDoT(
                configuration.Hostname!, 853, "payload.example", DnsRecordType.A, false, false, false,
                configuration, false, token));
            return Assert.IsType<DnsResponse>(failure.Response);
        }

        /// <summary>Caller-constructed wire queries retain the existing exception type and normalized lookup cause.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WireQueryHostnameFailureRetainsExceptionDiagnostics(bool tcp) {
            var cause = new SocketException((int)SocketError.HostNotFound);
            DnsServerResolver.ResolveHostAddressesAsync = _ => throw cause;
            var message = new DnsMessage("payload.example", DnsRecordType.A, new DnsMessageOptions());
            var failure = await Assert.ThrowsAsync<DnsClientException>(async () => {
                if (tcp) await DnsWireQueryClient.QueryTcpAsync("missing.example", 53, message);
                else await DnsWireQueryClient.QueryUdpAsync("missing.example", 53, message);
            });
            Assert.Same(cause, failure.InnerException);
            Assert.Equal(DnsQueryErrorCode.Network, failure.Response!.ErrorCode);
            Assert.Same(cause, failure.Response.Exception);
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
            Assert.Equal(DnsQueryErrorCode.None, stale.ErrorCode);
            Assert.Null(stale.Exception);
            Assert.True(configuration.ServerResolution!.UsedStaleAddress);
            Assert.Equal("lookup unavailable", configuration.ServerResolution.Error);
            Assert.Null(configuration.ServerResolution.BootstrapResolver);
            var cached = await DnsServerResolver.ResolveAsync("diagnostic.local", configuration, default);
            Assert.Equal(IPAddress.Loopback, cached.Address);
            Assert.Equal(DnsQueryErrorCode.None, cached.ErrorCode);
            Assert.Null(cached.Exception);
            Assert.True(configuration.ServerResolution.ServedFromCache);
            Assert.True(configuration.ServerResolution.UsedStaleAddress);
            Assert.Equal("lookup unavailable", configuration.ServerResolution.Error);
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

        /// <summary>A disconnected system refresh fails explicitly without publishing a partial configuration.</summary>
        [Fact]
        public void EmptySystemRefreshFailsAtomicallyAndAllowsExplicitFallback() {
            var servers = new System.Collections.Generic.List<string> { "192.0.2.1" };
            SystemInformation.SetDnsServerProvider(() => servers);
            try {
                var configuration = new Configuration(DnsEndpoint.System);
                SystemDnsConfiguration original = configuration.SystemDnsConfiguration!;
                servers = new System.Collections.Generic.List<string>();
                Assert.Throws<InvalidOperationException>(() => configuration.RefreshSystemDns());
                Assert.Same(original, configuration.SystemDnsConfiguration);
                Assert.Equal("192.0.2.1", configuration.Hostname);
                configuration.RefreshSystemDns(SystemDnsFallback.PublicResolvers);
                Assert.Equal(SystemDnsDiscoverySource.PublicFallback, configuration.SystemDnsConfiguration!.Source);
                Assert.Contains(configuration.Hostname!, configuration.SystemDnsConfiguration.DnsServers);
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

            var firstTask = DnsServerResolver.ResolveAsync(
                "concurrent.local",
                1000,
                CancellationToken.None);

            var secondTask = DnsServerResolver.ResolveAsync(
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
