using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the DNS failover selection strategy.
    /// </summary>
    public class FailoverStrategyTests {
        /// <summary>
        /// Ensures hostnames cycle correctly when failover is used.
        /// </summary>
        [Fact]
        public void SelectHostNameStrategy_ShouldUseCurrentIndex() {
            var config = new Configuration(DnsEndpoint.Cloudflare, DnsSelectionStrategy.Failover);
            // Cloudflare endpoints have two hostnames
            Assert.Equal("1.1.1.1", config.Hostname);

            typeof(Configuration).GetMethod("AdvanceToNextHostname", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(config, null);
            config.SelectHostNameStrategy();
            Assert.Equal("1.0.0.1", config.Hostname);

            typeof(Configuration).GetMethod("AdvanceToNextHostname", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(config, null);
            config.SelectHostNameStrategy();
            Assert.Equal("1.1.1.1", config.Hostname);
        }

        /// <summary>
        /// Verifies that retry logic advances the hostname after failure.
        /// </summary>
        [Fact]
        public async Task RetryAsync_ShouldAdvanceHostnameOnFailure() {
            var config = new Configuration(DnsEndpoint.Cloudflare, DnsSelectionStrategy.Failover);
            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
            var response = new DnsResponse { Status = DnsResponseCode.ServerFailure };
            Func<Task<DnsResponse>> action = () => Task.FromResult(response);
            var generic = method.MakeGenericMethod(typeof(DnsResponse));
            var advance = (Action)Delegate.CreateDelegate(typeof(Action), config, "AdvanceToNextHostname", false)!;
            var ex = await Assert.ThrowsAsync<DnsClientException>(async () =>
            {
                await (Task<DnsResponse>)generic.Invoke(null, new object?[] { action, 2, 1, advance, false, CancellationToken.None })!;
            });
            Assert.Same(response, ex.Response);

            config.SelectHostNameStrategy();
            Assert.Equal("1.0.0.1", config.Hostname);
        }
    }
}
