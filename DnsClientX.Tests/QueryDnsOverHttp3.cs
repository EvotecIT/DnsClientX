#if NET5_0_OR_GREATER
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests DNS over HTTP/3 endpoints.
    /// </summary>
    public class QueryDnsOverHttp3 {
        /// <summary>
        /// Ensures A record resolution works over HTTP/3.
        /// </summary>
        [Theory(Skip = "External dependency - network unreachable in CI")]
        [InlineData("1.1.1.1")]
        [InlineData("8.8.8.8")]
        public async Task ShouldResolveA(string hostName) {
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, hostName, DnsRequestFormat.DnsOverHttp3);
            Assert.NotEmpty(response.Answers);
        }
    }
}
#endif
