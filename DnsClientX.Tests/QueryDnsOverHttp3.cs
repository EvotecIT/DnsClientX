#if NET8_0_OR_GREATER
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class QueryDnsOverHttp3 {
        [Theory]
        [InlineData("1.1.1.1")]
        [InlineData("8.8.8.8")]
        public async Task ShouldResolveA(string hostName) {
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, hostName, DnsRequestFormat.DnsOverHttp3);
            Assert.NotEmpty(response.Answers);
        }
    }
}
#endif
