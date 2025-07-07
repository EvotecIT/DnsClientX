using System.Collections.Generic;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsServiceDiscoveryTests {
        [Fact]
        public void PropertiesStoreValues() {
            var discovery = new DnsServiceDiscovery {
                ServiceName = "_http._tcp.example.com",
                Target = "host.example.com",
                Port = 8080,
                Priority = 1,
                Weight = 2,
                Metadata = new Dictionary<string, string> { { "k", "v" } }
            };

            Assert.Equal("_http._tcp.example.com", discovery.ServiceName);
            Assert.Equal("host.example.com", discovery.Target);
            Assert.Equal(8080, discovery.Port);
            Assert.Equal(1, discovery.Priority);
            Assert.Equal(2, discovery.Weight);
            Assert.Equal("v", discovery.Metadata!["k"]);
        }
    }
}
