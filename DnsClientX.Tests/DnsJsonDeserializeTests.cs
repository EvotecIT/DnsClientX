using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using DnsClientX;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for JSON deserialization helper methods.
    /// </summary>
    public class DnsJsonDeserializeTests {
        /// <summary>Cloudflare JSON comments expose the same structured EDE evidence as wire/Google replies.</summary>
        [Theory]
        [InlineData("\"EDE(9): DNSKEY Missing no matching key\"", "EDE(9): DNSKEY Missing no matching key", 9, "DNSKEY Missing no matching key")]
        [InlineData("[\"EDE(15): Blocked by policy\"]", "EDE(15): Blocked by policy", 15, "Blocked by policy")]
        public async Task Deserialize_PromotesRecognizedEdeComments(string commentJson, string originalComment, int code, string text) {
            using var message = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"Status\":2,\"Comment\":" + commentJson + "}")
            };
            DnsResponse response = await message.DeserializeResponse();

            ExtendedDnsError error = Assert.Single(response.ExtendedDnsErrors);
            Assert.Equal(code, error.InfoCode);
            Assert.Equal(text, error.ExtraText);
            Assert.Equal(originalComment, response.Comments);
            Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
        }

        /// <summary>Multiple comment entries and reflection-based consumers retain each reported code.</summary>
        [Fact]
        public void Deserialize_MultipleEdeComments_PreservesEachError() {
            var response = DnsClientXJsonSerializer.Deserialize<DnsResponse>(
                "{\"Status\":0,\"Comment\":[\"EDE(3): Stale answer\",\"EDE(0): Additional context\"]}")!;

            Assert.Equal(new[] { 3, 0 }, response.ExtendedDnsErrors.Select(error => error.InfoCode));
            Assert.Equal(new[] { "Stale answer", "Additional context" }, response.ExtendedDnsErrors.Select(error => error.ExtraText));
            Assert.Equal("EDE(3): Stale answer; EDE(0): Additional context", response.Comments);
            Assert.Equal(DnsResponseCode.NoError, response.Status);
        }

        /// <summary>A malformed marker inside free-form EDE text is retained while later valid entries remain distinct.</summary>
        [Fact]
        public void Deserialize_MalformedMarkerWithinEdeTextPreservesContext() {
            var response = DnsClientXJsonSerializer.Deserialize<DnsResponse>(
                "{\"Comment\":[\"EDE(9): Missing key; EDE(note): resolver context\",\"EDE(0): Additional context\"]}")!;

            Assert.Equal(new[] { 9, 0 }, response.ExtendedDnsErrors.Select(error => error.InfoCode));
            Assert.Equal("Missing key; EDE(note): resolver context", response.ExtendedDnsErrors[0].ExtraText);
            Assert.Equal("Additional context", response.ExtendedDnsErrors[1].ExtraText);
            Assert.Equal("EDE(9): Missing key; EDE(note): resolver context; EDE(0): Additional context", response.Comments);
        }

        /// <summary>Structured provider data takes precedence, independent of JSON property order.</summary>
        [Theory]
        [InlineData("\"Comment\":\"EDE(9): comment text\",\"extended_dns_errors\":[{\"info_code\":6,\"extra_text\":\"authoritative detail\"}]")]
        [InlineData("\"extended_dns_errors\":[{\"info_code\":6,\"extra_text\":\"authoritative detail\"}],\"Comment\":\"EDE(9): comment text\"")]
        public void Deserialize_PreservesStructuredProviderErrors(string properties) {
            var response = DnsClientXJsonSerializer.Deserialize<DnsResponse>("{" + properties + "}")!;

            ExtendedDnsError error = Assert.Single(response.ExtendedDnsErrors);
            Assert.Equal(6, error.InfoCode);
            Assert.Equal("authoritative detail", error.ExtraText);
            Assert.Equal("EDE(9): comment text", response.Comments);
        }

        /// <summary>Ordinary prose and malformed/out-of-range codes do not become invented structured errors.</summary>
        [Theory]
        [InlineData("Resolver mentions EDE(9): in prose")]
        [InlineData("EDE(-1): invalid")]
        [InlineData("EDE(65536): invalid")]
        [InlineData("EDE(9) missing colon")]
        public void Deserialize_UnrecognizedComments_RemainText(string comment) {
            string json = System.Text.Json.JsonSerializer.Serialize(new { Comment = comment });
            var response = DnsClientXJsonSerializer.Deserialize<DnsResponse>(json)!;
            Assert.Empty(response.ExtendedDnsErrors);
            Assert.Equal(comment, response.Comments);
        }

        /// <summary>
        /// Ensures an exception is thrown when the HTTP response has no content.
        /// </summary>
        [Fact]
        public async Task Deserialize_ContentLengthZero_ThrowsException() {
            using var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(Array.Empty<byte>())
            };
            var ex = await Assert.ThrowsAsync<DnsClientException>(
                () => response.Deserialize(DnsJsonContext.Default.DnsResponse));
            Assert.Contains("Response content is empty", ex.Message);
        }

        /// <summary>A failed HTTP exchange cannot be accepted as a successful DNS answer.</summary>
        [Fact]
        public async Task Deserialize_HttpFailureWithValidJson_ThrowsException() {
            using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) {
                Content = new StringContent("{\"Status\":0}")
            };

            var ex = await Assert.ThrowsAsync<DnsClientException>(
                () => response.DeserializeResponse());
            Assert.Contains("HTTP 500", ex.Message);
        }
    }
}
