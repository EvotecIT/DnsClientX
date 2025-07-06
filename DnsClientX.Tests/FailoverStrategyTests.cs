using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class FailoverStrategyTests {
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

        [Fact]
        public async Task RetryAsync_ShouldAdvanceHostnameOnFailure() {
            var config = new Configuration(DnsEndpoint.Cloudflare, DnsSelectionStrategy.Failover);
            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
            Func<Task<DnsResponse>> action = () => Task.FromResult(new DnsResponse { Status = DnsResponseCode.ServerFailure });
            var generic = method.MakeGenericMethod(typeof(DnsResponse));
            var advance = (Action)Delegate.CreateDelegate(typeof(Action), config, "AdvanceToNextHostname", false)!;
            await Assert.ThrowsAsync<DnsClientException>(async () =>
            {
                await (Task<DnsResponse>)generic.Invoke(null, new object[] { action, 2, 1, advance, false, CancellationToken.None })!;
            });

            config.SelectHostNameStrategy();
            Assert.Equal("1.0.0.1", config.Hostname);
        }
    }
}
