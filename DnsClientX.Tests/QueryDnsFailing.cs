using System.Threading.Tasks;

namespace DnsClientX.Tests {
    public class QueryDnsFailing {
        [Theory]
        [InlineData("8.8.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("a1akam1.net", DnsRequestFormat.DnsOverUDP)]
        public async Task ShouldFailWithTimeout(string hostName, DnsRequestFormat requestFormat) {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.A, hostName, requestFormat, timeOutMilliseconds: 500);
            Assert.True(response.Status != DnsResponseCode.NoError);
        }


        [Theory]
        [InlineData("8.8.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("a1akam1.net", DnsRequestFormat.DnsOverUDP)]
        public async Task ShouldFailWithTimeoutResolve(string hostName, DnsRequestFormat requestFormat) {
            ClientX client = new ClientX(hostName, requestFormat) {
                Debug = true
            };
            var response = await client.Resolve("github.com", DnsRecordType.A);
            Assert.True(response.Status != DnsResponseCode.NoError);
        }
    }
}
