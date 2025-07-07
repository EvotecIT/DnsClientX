using System.Net;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsSrvRecordTests {
        [Fact]
        public void PropertiesStoreValues() {
            var record = new DnsSrvRecord {
                Target = "host.example.com",
                Port = 443,
                Priority = 1,
                Weight = 5,
                Addresses = new[] { IPAddress.Parse("1.1.1.1") }
            };

            Assert.Equal("host.example.com", record.Target);
            Assert.Equal(443, record.Port);
            Assert.Equal(1, record.Priority);
            Assert.Equal(5, record.Weight);
            Assert.Single(record.Addresses!, IPAddress.Parse("1.1.1.1"));
        }
    }
}
