using DnsClientX;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsJsonSerializationTests {
        [Fact]
        public void Serialize_UsesCamelCasePropertyNames() {
            var minimal = new DnsAnswerMinimal {
                Name = "example.com",
                TTL = 60,
                Type = DnsRecordType.A,
                Data = "1.1.1.1"
            };

            string json = DnsJson.Serialize(minimal);

            Assert.Contains("\"name\":\"example.com\"", json);
            Assert.Contains("\"type\":1", json);
            Assert.Contains("\"ttl\":60", json);
            Assert.Contains("\"data\":\"1.1.1.1\"", json);
        }
    }
}
