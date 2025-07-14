using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using DnsClientX;

namespace DnsClientX.Tests {
    public class DnsJsonDeserializeTests {
        [Fact]
        public async Task Deserialize_ContentLengthZero_ThrowsException() {
            using var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(Array.Empty<byte>())
            };
            var ex = await Assert.ThrowsAsync<DnsClientException>(
                () => response.Deserialize<object>());
            Assert.Contains("Response content is empty", ex.Message);
        }
    }
}
