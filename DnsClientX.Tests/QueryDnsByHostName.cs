using System.Threading.Tasks;

namespace DnsClientX.Tests {
    public class QueryDnsByHostName {
        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.DnsOverHttpsJSON)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]        [InlineData("208.67.222.222", DnsRequestFormat.DnsOverHttps)]
        public async Task ShouldWorkForTXT(string hostName, DnsRequestFormat requestFormat) {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.TXT, hostName, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.DnsOverHttpsJSON)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]
        [InlineData("208.67.222.222", DnsRequestFormat.DnsOverHttps)]
        public void ShouldWorkForTXTSync(string hostName, DnsRequestFormat requestFormat) {
            var response = ClientX.QueryDnsSync("github.com", DnsRecordType.TXT, hostName, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverTCP)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]        [InlineData("208.67.222.222", DnsRequestFormat.DnsOverHttps)]
        public async Task ShouldWorkForA(string hostName, DnsRequestFormat requestFormat) {
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, hostName, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverTCP)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]
        [InlineData("208.67.222.222", DnsRequestFormat.DnsOverHttps)]
        public void ShouldWorkForASync(string hostName, DnsRequestFormat requestFormat) {
            var response = ClientX.QueryDnsSync("evotec.pl", DnsRecordType.A, hostName, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverTCP)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]
        [InlineData("208.67.222.222", DnsRequestFormat.DnsOverHttps)]
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]        [InlineData("208.67.222.222", DnsRequestFormat.DnsOverHttps)]
        public async Task ShouldWorkForMultipleDomains(string hostName, DnsRequestFormat requestFormat) {
            var domains = new[] { "evotec.pl", "google.com" };
            var responses = await ClientX.QueryDns(domains, DnsRecordType.A, hostName, requestFormat);
            foreach (var domain in domains) {
                var response = responses.First(r => r.Questions.Any(q => q.Name == domain));
                foreach (DnsAnswer answer in response.Answers) {
                    Assert.True(answer.Name == domain);
                    Assert.True(answer.Type == DnsRecordType.A);
                    Assert.True(answer.Data.Length > 0);
                }
            }
        }

        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverTCP)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]
        [InlineData("208.67.222.222", DnsRequestFormat.DnsOverHttps)]
        public void ShouldWorkForMultipleDomainsSync(string hostName, DnsRequestFormat requestFormat) {
            var domains = new[] { "evotec.pl", "google.com" };
            var responses = ClientX.QueryDnsSync(domains, DnsRecordType.A, hostName, requestFormat);
            foreach (var domain in domains) {
                var response = responses.First(r => r.Questions.Any(q => q.Name == domain));
                foreach (DnsAnswer answer in response.Answers) {
                    Assert.True(answer.Name == domain);
                    Assert.True(answer.Type == DnsRecordType.A);
                    Assert.True(answer.Data.Length > 0);
                }
            }
        }
    }
}
