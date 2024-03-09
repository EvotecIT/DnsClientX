namespace DnsClientX.Tests {
    public class CompareJsonWithDnsWire {
        [Theory]
        [InlineData("evotec.pl", DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS, DnsRecordType.A)]
        [InlineData("reddit.com", DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS, DnsRecordType.A)]
        [InlineData("www.example.com", DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS, DnsRecordType.A)]
        [InlineData("evotec.pl", DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS, DnsRecordType.AAAA)]
        [InlineData("www.microsoft.com", DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS, DnsRecordType.CNAME)]
        public async void CompareAnswersRecord(string name, DnsEndpoint endpoint, DnsEndpoint endpointCompare, DnsRecordType resourceRecordType) {
            var Client = new DnsClientX(endpoint);
            DnsAnswer[] aAnswers = await Client.ResolveAll(name, resourceRecordType);

            var ClientWire = new DnsClientX(endpointCompare);
            DnsAnswer[] aAnswersWire = await ClientWire.ResolveAll(name, resourceRecordType);

            // Sort the arrays by Name, Type, and Data
            var sortedAAnswers = aAnswers.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();
            var sortedAAnswersWire = aAnswersWire.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();

            // Check that the arrays have the same length
            Assert.True(sortedAAnswers.Length == sortedAAnswersWire.Length);

            // Check that the arrays have the same elements in the same order
            for (int i = 0; i < sortedAAnswers.Length; i++) {
                Assert.True((bool)(sortedAAnswers[i].Name == sortedAAnswersWire[i].Name));
                Assert.True((bool)(sortedAAnswers[i].Type == sortedAAnswersWire[i].Type));
                Assert.True((bool)(sortedAAnswers[i].Data.Length == sortedAAnswersWire[i].Data.Length));
                Assert.True(Enumerable.SequenceEqual<char>(sortedAAnswers[i].Data, sortedAAnswersWire[i].Data));
            }
        }
    }
}
