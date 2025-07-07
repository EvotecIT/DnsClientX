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
    }
}
