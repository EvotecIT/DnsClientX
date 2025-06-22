using Xunit.Abstractions;

namespace DnsClientX.Tests {
    public class CompareProvidersResolve(ITestOutputHelper output) {
        [Theory]
        //[MemberData(nameof(TestData))]
        [InlineData("evotec.pl", DnsRecordType.A)]
        [InlineData("www.bücher.de", DnsRecordType.A)]
        [InlineData("evotec.pl", DnsRecordType.SOA)]
        [InlineData("evotec.pl", DnsRecordType.DNSKEY)]
        [InlineData("sip.evotec.pl", DnsRecordType.CNAME)]
        [InlineData("autodiscover.evotec.pl", DnsRecordType.CNAME)]
        [InlineData("evotec.pl", DnsRecordType.CAA)]
        [InlineData("evotec.pl", DnsRecordType.AAAA)]
        [InlineData("evotec.pl", DnsRecordType.MX)]
        [InlineData("evotec.pl", DnsRecordType.NS)]
        [InlineData("evotec.pl", DnsRecordType.SPF)]
        [InlineData("evotec.pl", DnsRecordType.TXT)]
        [InlineData("evotec.pl", DnsRecordType.SRV)]
        [InlineData("evotec.pl", DnsRecordType.NSEC, new[] { DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily, DnsEndpoint.Quad9ECS, DnsEndpoint.Quad9, DnsEndpoint.Quad9Unsecure })]
        [InlineData("cloudflare.com", DnsRecordType.NSEC)]
        [InlineData("mail-db3pr0202cu00100.inbound.protection.outlook.com", DnsRecordType.PTR)]
        // lets try different sites
        [InlineData("reddit.com", DnsRecordType.A)]
        [InlineData("reddit.com", DnsRecordType.CAA)]
        [InlineData("reddit.com", DnsRecordType.SOA)]
        // github.com has a lot of TXT records, including multiline, however google dns doesn't do multiline TXT records and delivers them as one line
        [InlineData("github.com", DnsRecordType.TXT, new[] { DnsEndpoint.Google })]

        [InlineData("microsoft.com", DnsRecordType.MX)]
        [InlineData("microsoft.com", DnsRecordType.NS)]

        [InlineData("google.com", DnsRecordType.MX)]
        [InlineData("1.1.1.1", DnsRecordType.PTR)]
        [InlineData("108.138.7.68", DnsRecordType.PTR)]
        [InlineData("sip2sip.info", DnsRecordType.NAPTR)]
        public async Task CompareRecords(string name, DnsRecordType resourceRecordType, DnsEndpoint[]? excludedEndpoints = null) {
            output.WriteLine($"Testing record: {name}, type: {resourceRecordType}");

            var primaryEndpoint = DnsEndpoint.Cloudflare;

            var Client = new ClientX(primaryEndpoint);
            DnsResponse aAnswersPrimary = await Client.Resolve(name, resourceRecordType);

            foreach (var endpointCompare in Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>()) {
                if (endpointCompare == primaryEndpoint) {
                    continue;
                }
                if (excludedEndpoints != null && excludedEndpoints.Contains(endpointCompare)) {
                    continue;
                }

                output.WriteLine("Provider: " + endpointCompare.ToString());

                var ClientToCompare = new ClientX(endpointCompare);
                DnsResponse aAnswersToCompare = await ClientToCompare.Resolve(name, resourceRecordType);

                var sortedAAnswers = aAnswersPrimary.Answers.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();
                var sortedAAnswersCompared = aAnswersToCompare.Answers.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();

                // Check that the arrays have the same length
                output.WriteLine($"Answer count expected {sortedAAnswers.Length} vs {sortedAAnswersCompared.Length}");
                Assert.Equal(sortedAAnswers.Length, sortedAAnswersCompared.Length);

                // Check that the arrays have the same elements in the same order
                for (int i = 0; i < sortedAAnswers.Length; i++) {

                    output.WriteLine($"Record {i} should equal: {sortedAAnswers[i].Data} == {sortedAAnswersCompared[i].Data}");
                    Assert.Equal(sortedAAnswers[i].Name, sortedAAnswersCompared[i].Name);
                    Assert.Equal(sortedAAnswers[i].Type, sortedAAnswersCompared[i].Type);
                    Assert.Equal(sortedAAnswers[i].Data.Length, sortedAAnswersCompared[i].Data.Length);
                    Assert.Equal(sortedAAnswers[i].Data, sortedAAnswersCompared[i].Data);
                    Assert.Equal(sortedAAnswers[i].DataStrings.Length, sortedAAnswersCompared[i].DataStrings.Length);
                }

                var sortedQuestions = (aAnswersPrimary.Questions ?? Array.Empty<DnsQuestion>())
                    .OrderBy(a => a.Name)
                    .ThenBy(a => a.Type)
                    .ThenBy(a => a.Type)
                    .ToArray();
                var sortedQuestionsCompared = (aAnswersToCompare.Questions ?? Array.Empty<DnsQuestion>())
                    .OrderBy(a => a.Name)
                    .ThenBy(a => a.Type)
                    .ThenBy(a => a.Type)
                    .ToArray();

                // Check that the arrays have the same length
                output.WriteLine($"Question count expected {sortedQuestions.Length} vs {sortedQuestionsCompared.Length}");
                Assert.Equal(sortedQuestions.Length, sortedQuestionsCompared.Length);

                for (int i = 0; i < sortedQuestions.Length; i++) {
                    output.WriteLine("Provider: " + endpointCompare.ToString());
                    output.WriteLine($"Question {i} should equal: {sortedQuestions[i].Name} == {sortedQuestionsCompared[i].Name}");
                    Assert.True(sortedQuestions[i].Name == sortedQuestionsCompared[i].Name, $"Provider {endpointCompare}. There is a name mismatch for " + sortedQuestions[i].Name);
                    Assert.True((bool)(sortedQuestions[i].Type == sortedQuestionsCompared[i].Type), $"Provider {endpointCompare}. There is a type mismatch for " + sortedQuestions[i].Type);
                }
            }
        }
    }
}
