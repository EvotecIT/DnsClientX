using System.Collections.Generic;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the <see cref="DnsService"/> data type.
    /// </summary>
    public class DnsServiceTests {
        /// <summary>
        /// Verifies that properties are correctly stored and returned.
        /// </summary>
        [Fact]
        public void PropertiesStoreValues() {
            var svc = new DnsService {
                ServiceName = "_http._tcp.example.com",
                Target = "host.example.com",
                Port = 8080,
                Priority = 1,
                Weight = 2,
                Metadata = new Dictionary<string, string> { { "k", "v" } }
            };

            Assert.Equal("_http._tcp.example.com", svc.ServiceName);
            Assert.Equal("host.example.com", svc.Target);
            Assert.Equal(8080, svc.Port);
            Assert.Equal(1, svc.Priority);
            Assert.Equal(2, svc.Weight);
            Assert.Equal("v", svc.Metadata!["k"]);
        }
    }
}
