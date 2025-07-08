using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    public class RootServersTests {
        [Fact]
        public void RootServersList_HasExpectedCounts() {
            var servers = RootServers.Servers;
            Assert.Equal(26, servers.Length);
            Assert.Equal(13, servers.Count(s => !s.Contains(':')));
            Assert.Equal(13, servers.Count(s => s.Contains(':')));
        }

        [Fact]
        public void RootServersList_HasOnlyUniqueValues() {
            var servers = RootServers.Servers;
            var ipv4Servers = servers.Where(s => !s.Contains(':')).ToArray();
            var ipv6Servers = servers.Where(s => s.Contains(':')).ToArray();

            Assert.Equal(ipv4Servers.Length, ipv4Servers.Distinct().Count());
            Assert.Equal(ipv6Servers.Length, ipv6Servers.Distinct().Count());
            Assert.Equal(servers.Length, servers.Distinct().Count());
        }
    }
}
