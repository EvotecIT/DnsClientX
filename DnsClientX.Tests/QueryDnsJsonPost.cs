using System;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class QueryDnsJsonPost {
        [Theory]
        [InlineData(DnsEndpoint.CloudflareJsonPost)]
        [InlineData(DnsEndpoint.GoogleJsonPost)]
        public async Task ShouldWorkForA(DnsEndpoint endpoint) {
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, endpoint);
            Assert.NotEmpty(response.Answers);
        }
    }
}
