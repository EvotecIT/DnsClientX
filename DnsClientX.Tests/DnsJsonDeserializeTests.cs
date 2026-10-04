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
