using DnsClientX;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the streaming variant of resolve operations.
    /// </summary>
    public class ResolveStream {
        /// <summary>
        /// Streams multiple responses and verifies the count.
        /// </summary>
        [Fact]
        public async Task ShouldStreamMultipleResponses() {
            using var client = new ClientX(DnsEndpoint.System);
            var names = new[] { "github.com", "microsoft.com" };
            var types = new[] { DnsRecordType.A, DnsRecordType.MX };
            int count = 0;

            await foreach (var response in client.ResolveStream(names, types, retryOnTransient: false)) {
                Assert.NotNull(response);
                Assert.NotNull(response.Answers);
                count++;
            }

            Assert.Equal(names.Length * types.Length, count);
        }
    }
}
