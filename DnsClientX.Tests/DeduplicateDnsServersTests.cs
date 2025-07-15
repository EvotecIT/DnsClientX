using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            MethodInfo method = typeof(SystemInformation).GetMethod("DeduplicateDnsServers", BindingFlags.NonPublic | BindingFlags.Static)!;
            var input = new List<string> { "1.1.1.1", "1.1.1.1", "[2001:db8::1]", "[2001:db8::1]" };
            var result = (List<string>)method.Invoke(null, new object[] { input })!;
            Assert.Equal(new[] { "1.1.1.1", "[2001:db8::1]" }, result);
        }

        /// <summary>
        /// The original order of unique entries should remain after deduplication.
        /// </summary>
        [Fact]
        public void DuplicateDnsServers_OrderIsPreserved() {
            MethodInfo method = typeof(SystemInformation).GetMethod("DeduplicateDnsServers", BindingFlags.NonPublic | BindingFlags.Static)!;
            var input = new List<string> { "2.2.2.2", "1.1.1.1", "2.2.2.2", "[2001:db8::1]", "1.1.1.1" };
            var result = (List<string>)method.Invoke(null, new object[] { input })!;
            Assert.Equal(new[] { "2.2.2.2", "1.1.1.1", "[2001:db8::1]" }, result);
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
