namespace DnsClientX.Tests {
    /// <summary>
    /// Tests resolving records by specifying host name and request format.
    /// </summary>
    public class QueryDnsByHostName {
        /// <summary>
        /// Ensures TXT queries succeed when specifying the DNS host.
        /// </summary>
        /// <summary>
        /// Ensures multiple domains can be resolved for a given host name.
        /// </summary>
        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.DnsOverHttpsJSON)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]
        [InlineData("208.67.222.222", DnsRequestFormat.DnsOverHttps)]
        public async Task ShouldWorkForTXT(string hostName, DnsRequestFormat requestFormat) {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.TXT, hostName, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

        /// <summary>
        /// Ensures A record queries succeed when specifying the DNS host.
        /// </summary>
        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverTCP)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]
        [InlineData("208.67.222.222", DnsRequestFormat.DnsOverHttps)]
        public async Task ShouldWorkForA(string hostName, DnsRequestFormat requestFormat) {
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, hostName, requestFormat);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }

        /// <summary>
        /// Ensures multiple domains can be resolved for a given host name.
        /// </summary>
        [Theory]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("family.cloudflare-dns.com", DnsRequestFormat.DnsOverHttpsJSON)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverUDP)]
        [InlineData("1.1.1.1", DnsRequestFormat.DnsOverTCP)]
        // Google contrary to the other endpoints does not work with /dns-query but with /resolve
        // [InlineData("8.8.8.8", DnsRequestFormat.JSON)]
        [InlineData("208.67.222.222", DnsRequestFormat.DnsOverHttps)]
        public async Task ShouldWorkForMultipleDomains(string hostName, DnsRequestFormat requestFormat) {
            var domains = new[] { "evotec.pl", "google.com" };
            var responses = await ClientX.QueryDns(domains, DnsRecordType.A, hostName, requestFormat);
            foreach (var domain in domains) {
                var response = responses.First(r => r.Questions?.Any(q => q.Name == domain) == true);
                foreach (DnsAnswer answer in response.Answers) {
                    Assert.True(answer.Name == domain);
                    Assert.True(answer.Type == DnsRecordType.A);
                    Assert.True(answer.Data.Length > 0);
                }
            }
        }
    }
}
