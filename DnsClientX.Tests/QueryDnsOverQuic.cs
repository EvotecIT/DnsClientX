#if DNS_OVER_QUIC && NET8_0_OR_GREATER
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class QueryDnsOverQuic {
        [Theory]
        [InlineData(DnsEndpoint.CloudflareQuic)]
        [InlineData(DnsEndpoint.GoogleQuic)]
        public async Task ShouldResolveA(DnsEndpoint endpoint) {
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, endpoint);
            Assert.NotEmpty(response.Answers);
        }
    }
}
#endif
