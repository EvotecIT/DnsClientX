using System;

namespace DnsClientX.Tests {
    public class QueryDnsByUri {
        [Theory]
        [InlineData("https://1.1.1.1/dns-query", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://8.8.8.8/resolve", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://9.9.9.11:5053/dns-query", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://208.67.222.123/dns-query", DnsRequestFormat.DnsOverHttps)]
        public async void ShouldWorkForTXT(string baseUri, DnsRequestFormat requestFormat) {
            Uri uri = new Uri(baseUri);
            var response = await ClientX.QueryDns("github.com", DnsRecordType.TXT, uri, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData("https://1.1.1.1/dns-query", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://8.8.8.8/resolve", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://9.9.9.11:5053/dns-query", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://208.67.222.123/dns-query", DnsRequestFormat.DnsOverHttps)]
        public void ShouldWorkForTXTSync(string baseUri, DnsRequestFormat requestFormat) {
            Uri uri = new Uri(baseUri);
            var response = ClientX.QueryDnsSync("github.com", DnsRecordType.TXT, uri, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData("https://1.1.1.1/dns-query", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://8.8.8.8/resolve", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://9.9.9.10:5053/dns-query", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://doh.opendns.com/dns-query", DnsRequestFormat.DnsOverHttps)]
        [InlineData("https://dns.adguard.com/dns-query", DnsRequestFormat.DnsOverHttps)]
        public async void ShouldWorkForA(string baseUri, DnsRequestFormat requestFormat) {
            Uri uri = new Uri(baseUri);
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, uri, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData("https://1.1.1.1/dns-query", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://8.8.8.8/resolve", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://9.9.9.10:5053/dns-query", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("https://doh.opendns.com/dns-query", DnsRequestFormat.DnsOverHttps)]
        [InlineData("https://dns.adguard.com/dns-query", DnsRequestFormat.DnsOverHttps)]
        public void ShouldWorkForASync(string baseUri, DnsRequestFormat requestFormat) {
            Uri uri = new Uri(baseUri);
            var response = ClientX.QueryDnsSync("evotec.pl", DnsRecordType.A, uri, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }
    }
}
