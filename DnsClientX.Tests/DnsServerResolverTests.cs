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
    }
}
