using System;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests querying DNS endpoints by providing the full request URI.
    /// </summary>
    public class QueryDnsByUri {
        /// <summary>
        /// Ensures TXT queries succeed when the endpoint is specified via URI.
        /// </summary>
        [Theory]
        [InlineData("https://1.1.1.1/dns-query", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://8.8.8.8/resolve", DnsRequestFormat.DnsOverHttpsJSON)]
        //[InlineData("https://9.9.9.11/dns-query", DnsRequestFormat.DnsOverHttps)]
        [InlineData("https://208.67.222.123/dns-query", DnsRequestFormat.DnsOverHttps)]
        public async Task ShouldWorkForTXT(string baseUri, DnsRequestFormat requestFormat) {
            Uri uri = new Uri(baseUri);
            var response = await ClientX.QueryDns("github.com", DnsRecordType.TXT, uri, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

        /// <summary>
        /// Ensures A record queries succeed when the endpoint is specified via URI.
        /// </summary>
        [Theory]
        [InlineData("https://1.1.1.1/dns-query", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://8.8.8.8/resolve", DnsRequestFormat.DnsOverHttpsJSON)]
        //[InlineData("https://9.9.9.10/dns-query", DnsRequestFormat.DnsOverHttps)]
        [InlineData("https://doh.opendns.com/dns-query", DnsRequestFormat.DnsOverHttps)]
        [InlineData("https://dns.adguard.com/dns-query", DnsRequestFormat.DnsOverHttps)]
        public async Task ShouldWorkForA(string baseUri, DnsRequestFormat requestFormat) {
            Uri uri = new Uri(baseUri);
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, uri, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }
    }
}
