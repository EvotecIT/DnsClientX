using System.Net.Http;
using System.Text;
using DnsClientX;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for JSON serialization utilities.
    /// </summary>
    public class DnsJsonSerializationTests {
        /// <summary>
        /// Verifies that serialized property names use camelCase.
        /// </summary>
        [Fact]
        public void Serialize_UsesCamelCasePropertyNames() {
            var minimal = new DnsAnswerMinimal {
                Name = "example.com",
                TTL = 60,
                Type = DnsRecordType.A,
                Data = "1.1.1.1"
            };

            string json = DnsJson.Serialize(minimal, DnsJsonContext.Default.DnsAnswerMinimal);

            Assert.Contains("\"name\":\"example.com\"", json);
            Assert.Contains("\"type\":1", json);
            Assert.Contains("\"ttl\":60", json);
            Assert.Contains("\"data\":\"1.1.1.1\"", json);
        }

        /// <summary>
        /// Ensure deserialization surfaces JsonException details via DnsClientException wrapping.
        /// </summary>
        [Fact]
        public async Task Deserialize_InvalidJson_WrapsJsonException() {
            using var msg = new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new StringContent("{ not-json }", Encoding.UTF8, "application/json")
            };

            var ex = await Assert.ThrowsAsync<DnsClientException>(async () =>
                await msg.Deserialize(DnsJsonContext.Default.DnsResponse));

            Assert.Contains("JsonException", ex.Message);
            Assert.IsType<System.Text.Json.JsonException>(ex.InnerException);
        }
    }
}
