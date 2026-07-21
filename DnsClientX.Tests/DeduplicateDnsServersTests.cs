using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for deduplicating DNS server lists.
    /// </summary>
    public class DeduplicateDnsServersTests {
        /// <summary>
        /// Duplicate addresses should be removed from the list.
        /// </summary>
        [Fact]
        public void DuplicateDnsServers_AreRemoved() {
            var input = new List<string> { "1.1.1.1", "1.1.1.1", "[2001:db8::1]", "[2001:db8::1]" };
            var configuration = new SystemDnsConfiguration(
                input,
                searchDomains: null,
                ndots: 1,
                SystemDnsDiscoverySource.CustomProvider);

            Assert.Equal(new[] { "1.1.1.1", "[2001:db8::1]" }, configuration.DnsServers);
        }

        /// <summary>
        /// The original order of unique entries should remain after deduplication.
        /// </summary>
        [Fact]
        public void DuplicateDnsServers_OrderIsPreserved() {
            var input = new List<string> { "2.2.2.2", "1.1.1.1", "2.2.2.2", "[2001:db8::1]", "1.1.1.1" };
            var configuration = new SystemDnsConfiguration(
                input,
                searchDomains: null,
                ndots: 1,
                SystemDnsDiscoverySource.CustomProvider);

            Assert.Equal(new[] { "2.2.2.2", "1.1.1.1", "[2001:db8::1]" }, configuration.DnsServers);
        }

        /// <summary>
        /// <see cref="SystemInformation.GetDnsFromActiveNetworkCard"/> should return a distinct list.
        /// </summary>
        [Fact]
        public void GetDnsFromActiveNetworkCard_ReturnsDistinctList() {
            var servers = SystemInformation.GetDnsFromActiveNetworkCard(refresh: true);
            var distinct = servers.Distinct().ToList();
            Assert.Equal(distinct, servers);
        }
    }
}
