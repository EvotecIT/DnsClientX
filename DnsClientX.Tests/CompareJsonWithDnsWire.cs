namespace DnsClientX.Tests {
    public class CompareJsonWithDnsWire {
        [Theory(Skip = "External dependency - unreliable for automated testing")]
        [InlineData("evotec.pl", DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS, DnsRecordType.A)]
        [InlineData("reddit.com", DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS, DnsRecordType.A)]
        // Removed www.example.com as it uses Akamai CDN which returns different IPs based on geographic location
        // [InlineData("www.example.com", DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS, DnsRecordType.A)]
        [InlineData("evotec.pl", DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS, DnsRecordType.AAAA)]
        [InlineData("www.microsoft.com", DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS, DnsRecordType.CNAME)]
        public async Task CompareAnswersRecord(string name, DnsEndpoint endpoint, DnsEndpoint endpointCompare, DnsRecordType resourceRecordType) {
            using var Client = new ClientX(endpoint);
            DnsAnswer[] aAnswers = await Client.ResolveAll(name, resourceRecordType);

            using var ClientWire = new ClientX(endpointCompare);
            DnsAnswer[] aAnswersWire = await ClientWire.ResolveAll(name, resourceRecordType);

            // Sort the arrays by Name, Type, and Data
            var sortedAAnswers = aAnswers.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();
            var sortedAAnswersWire = aAnswersWire.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();

            // Check that the arrays have the same length
            Assert.Equal(sortedAAnswers.Length, sortedAAnswersWire.Length);

            // Check that the arrays have the same elements in the same order
            for (int i = 0; i < sortedAAnswers.Length; i++) {
                Assert.Equal(sortedAAnswers[i].Name, sortedAAnswersWire[i].Name);
                Assert.Equal(sortedAAnswers[i].Type, sortedAAnswersWire[i].Type);
                Assert.Equal(sortedAAnswers[i].Data.Length, sortedAAnswersWire[i].Data.Length);
                Assert.Equal(sortedAAnswers[i].Data, sortedAAnswersWire[i].Data);
            }
        }
    }
}

