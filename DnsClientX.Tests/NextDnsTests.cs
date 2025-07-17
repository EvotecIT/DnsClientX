using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Integration tests targeting the NextDNS public resolver.
    /// </summary>
    public class NextDnsTests {
        /// <summary>
        /// Verifies that records can be resolved against NextDNS.
        /// </summary>
        [Fact]
        public async Task ResolveUsingNextDns() {
            using var client = new ClientX(DnsEndpoint.NextDNS) { Debug = false };
            var response = await client.Resolve("github.com", DnsRecordType.A);
            Assert.Equal(DnsResponseCode.NoError, response.Status);
            Assert.NotNull(response.Answers);
            Assert.True(response.Answers!.Length > 0);
        }
    }
}
