namespace DnsClientX.Tests {
    public class QueryDnsByHostName {
        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.JSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.JSON)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]
        [InlineData("208.67.222.222", DnsRequestFormat.WireFormatGet)]
        public async void ShouldWorkForTXT(string hostName, DnsRequestFormat requestFormat) {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.TXT, hostName, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.JSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.JSON)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]
        [InlineData("208.67.222.222", DnsRequestFormat.WireFormatGet)]
        public async void ShouldWorkForA(string hostName, DnsRequestFormat requestFormat) {
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, hostName, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }
    }
}
