using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests regarding the built-in list of DNS root servers.
    /// </summary>
    public class RootServersTests {
        /// <summary>
        /// Ensures the number of IPv4 and IPv6 servers matches expectations.
        /// </summary>
        [Fact]
        public void RootServersList_HasExpectedCounts() {
            var servers = RootServers.Servers;
            Assert.Equal(26, servers.Length);
            Assert.Equal(13, servers.Count(s => !s.Contains(':')));
            Assert.Equal(13, servers.Count(s => s.Contains(':')));
        }

        /// <summary>
        /// Verifies that the list of root servers contains only unique addresses.
        /// </summary>
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
