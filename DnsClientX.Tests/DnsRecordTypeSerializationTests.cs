using System.Text.Json;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests JSON serialization behavior for <see cref="DnsRecordType"/> values.
    /// </summary>
    public class DnsRecordTypeSerializationTests {
        /// <summary>
        /// Ensures values round-trip through serialization.
        /// </summary>
        [Theory]
        [InlineData(DnsRecordType.SVCB, 64)]
        [InlineData(DnsRecordType.HTTPS, 65)]
        public void SerializeDeserialize_RoundTrips(DnsRecordType type, int expected) {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = type,
                TTL = 60,
                DataRaw = "value"
            };

            string json = JsonSerializer.Serialize(answer);
            Assert.Contains($"\"type\":{expected}", json);

            var deserialized = JsonSerializer.Deserialize<DnsAnswer>(json)!;
            Assert.Equal(type, deserialized.Type);
        }

        /// <summary>
        /// Verifies that converting data leaves raw values intact for SVCB/HTTPS.
        /// </summary>
        [Theory]
        [InlineData(DnsRecordType.SVCB)]
        [InlineData(DnsRecordType.HTTPS)]
        public void ConvertData_ReturnsRaw(DnsRecordType type) {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = type,
                TTL = 60,
                DataRaw = "1 . alpn=\"h2\""
            };

            Assert.Equal(answer.DataRaw, answer.Data);
        }
    }
}
