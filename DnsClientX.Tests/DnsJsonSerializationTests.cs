using System.Net.Http;
using System.Text;
using DnsClientX;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for JSON serialization utilities.
    /// </summary>
    public class DnsJsonSerializationTests {
        /// <summary>Machine output adds normalized values without replacing the original record presentation.</summary>
        [Theory]
        [InlineData(DnsRecordType.CNAME, "Target.Example.", "target.example")]
        [InlineData(DnsRecordType.TXT, "\"one\" \" two\"", "one two")]
        [InlineData((DnsRecordType)65280, "Opaque CASE", "Opaque CASE")]
        public void Serialize_ResponseIncludesRawAndNormalizedData(DnsRecordType type, string raw, string normalized) {
            var response = new DnsResponse { Answers = new[] { new DnsAnswer { Name = "example.com", Type = type, DataRaw = raw } } };
            using var json = System.Text.Json.JsonDocument.Parse(DnsClientXJsonSerializer.Serialize(response));
            var answer = json.RootElement.GetProperty("Answer")[0];

            Assert.Equal(raw, answer.GetProperty("data").GetString());
            Assert.Equal(normalized, answer.GetProperty("data_normalized").GetString());
        }

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
