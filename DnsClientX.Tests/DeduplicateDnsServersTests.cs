using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class DeduplicateDnsServersTests {
        [Fact]
        public void DuplicateDnsServers_AreRemoved() {
            MethodInfo method = typeof(SystemInformation).GetMethod("DeduplicateDnsServers", BindingFlags.NonPublic | BindingFlags.Static)!;
            var input = new List<string> { "1.1.1.1", "1.1.1.1", "[2001:db8::1]", "[2001:db8::1]" };
            var result = (List<string>)method.Invoke(null, new object[] { input })!;
            Assert.Equal(new[] { "1.1.1.1", "[2001:db8::1]" }, result);
        }

        [Fact]
        public void GetDnsFromActiveNetworkCard_ReturnsDistinctList() {
            var servers = SystemInformation.GetDnsFromActiveNetworkCard(refresh: true);
            var distinct = servers.Distinct().ToList();
            Assert.Equal(distinct, servers);
        }
    }
}
