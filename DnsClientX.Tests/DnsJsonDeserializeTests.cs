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
    }
}
