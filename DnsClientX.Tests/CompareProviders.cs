using Xunit.Abstractions;

namespace DnsClientX.Tests {
    public class CompareProviders(ITestOutputHelper output) {
        //public static IEnumerable<object[]> TestData {
        //    get {
        //        // endpoints to exclude from testing temporary because of not yet fixed WireFormat
        //        var excludeEndpoints = new HashSet<DnsEndpoint> {
        //            // DnsEndpoint.OpenDNS,
        //            // DnsEndpoint.OpenDNSFamily,
        //            // DnsEndpoint.CloudflareWireFormat
        //        };

        //        var excludeEndpointsWithGoogle = new HashSet<DnsEndpoint> {
        //            //DnsEndpoint.OpenDNS,
        //            //DnsEndpoint.OpenDNSFamily,
        //            //DnsEndpoint.CloudflareWireFormat,
        //            DnsEndpoint.Google
        //        };

        //        var list = new List<object[]> {
        //            new object[] { "evotec.pl", ResourceRecordType.A, excludeEndpoints},
        //            new object[] { "evotec.pl", ResourceRecordType.CAA, excludeEndpoints },
        //            new object[] { "evotec.pl", ResourceRecordType.AAAA, excludeEndpoints },
        //            new object[] { "evotec.pl", ResourceRecordType.MX, excludeEndpoints },
        //            new object[] { "evotec.pl", ResourceRecordType.NS, excludeEndpoints },
        //            new object[] { "evotec.pl", ResourceRecordType.SPF, excludeEndpoints },
        //            new object[] { "evotec.pl", ResourceRecordType.TXT, excludeEndpoints },
        //            // lets try different sites
        //            new object[] {"reddit.com", ResourceRecordType.A, excludeEndpoints },
        //            new object[] {"reddit.com", ResourceRecordType.CAA, excludeEndpoints },
        //            // github.com has a lot of TXT records, including multiline
        //            // however google dns doesn't do multiline TXT records
        //            new object[] {"github.com", ResourceRecordType.TXT, excludeEndpointsWithGoogle },
        //        };
        //        return list;
        //    }
        //}


        [Theory]
        //[MemberData(nameof(TestData))]
        [InlineData("evotec.pl", DnsRecordType.A)]
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
        [InlineData("evotec.pl", DnsRecordType.NSEC)]
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
        public async void CompareRecords(string name, DnsRecordType resourceRecordType, DnsEndpoint[]? excludedEndpoints = null) {
            output.WriteLine($"Testing record: {name}, type: {resourceRecordType}");

            var primaryEndpoint = DnsEndpoint.Cloudflare;

            var Client = new DnsClientX(primaryEndpoint);
            DnsAnswer[] aAnswersPrimary = await Client.ResolveAll(name, resourceRecordType);

            foreach (var endpointCompare in Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>()) {
                if (endpointCompare == primaryEndpoint) {
                    continue;
                }
                if (excludedEndpoints != null && excludedEndpoints.Contains(endpointCompare)) {
                    continue;
                }
                var ClientToCompare = new DnsClientX(endpointCompare);
                DnsAnswer[] aAnswersToCompare = await ClientToCompare.ResolveAll(name, resourceRecordType);

                var sortedAAnswers = aAnswersPrimary.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();
                var sortedAAnswersCompared = aAnswersToCompare.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();

                // Check that the arrays have the same length
                Assert.True(sortedAAnswers.Length == sortedAAnswersCompared.Length);

                // Check that the arrays have the same elements in the same order
                for (int i = 0; i < sortedAAnswers.Length; i++) {
                    output.WriteLine("Provider: " + endpointCompare.ToString());
                    output.WriteLine($"Record {i} should equal: {sortedAAnswers[i].Data} == {sortedAAnswersCompared[i].Data}");
                    Assert.True((bool)(sortedAAnswers[i].Name == sortedAAnswersCompared[i].Name), $"Provider {endpointCompare}. There is a name mismatch for " + sortedAAnswers[i].Data);
                    Assert.True((bool)(sortedAAnswers[i].Type == sortedAAnswersCompared[i].Type), $"Provider {endpointCompare}. There is a type mismatch for " + sortedAAnswers[i].Data);
                    Assert.True((bool)(sortedAAnswers[i].Data.Length == sortedAAnswersCompared[i].Data.Length), $"Provider {endpointCompare}. Records not matching length for " + sortedAAnswers[i].Data + " length expected: " + sortedAAnswers[i].Data.Length + " length provided: " + sortedAAnswersCompared[i].Data.Length);
                    Assert.True(Enumerable.SequenceEqual<char>(sortedAAnswers[i].Data, sortedAAnswersCompared[i].Data), $"Provider {endpointCompare}. Records not matching content for " + sortedAAnswers[i].Data);
                    Assert.True((bool)(sortedAAnswers[i].DataStrings.Length == sortedAAnswersCompared[i].DataStrings.Length), $"Provider {endpointCompare}. Records not matching length for " + sortedAAnswers[i].DataStrings + " length expected: " + sortedAAnswers[i].DataStrings.Length + " length provided: " + sortedAAnswersCompared[i].DataStrings.Length);
                }
            }
        }

        [Theory]
        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.CloudflareFamily)]
        // Google seems to have a different set of TXT records for github.com, as it doesn't follow specficiation directly
        // It seems to merge multiline TXT records into one line
        // https://dns.google/query?name=github.com&rr_type=TXT&ecs=
        //[InlineData("github.com", ResourceRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.Google)]
        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.Quad9)]
        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.Quad9ECS)]
        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.Quad9Unsecure)]
        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.CloudflareWireFormat)]
        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS)]
        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNSFamily)]
        public async void CompareRecordTextMultiline(string name, DnsRecordType resourceRecordType, DnsEndpoint primaryEndpoint, DnsEndpoint endpointCompare) {
            var Client = new DnsClientX(primaryEndpoint);
            DnsAnswer[] aAnswersPrimary = await Client.ResolveAll(name, resourceRecordType);
            var ClientToCompare = new DnsClientX(endpointCompare);
            DnsAnswer[] aAnswersToCompare = await ClientToCompare.ResolveAll(name, resourceRecordType);

            // we focus only on SPF1 TXT records
            aAnswersPrimary = aAnswersPrimary.Where(a => a.Type == DnsRecordType.TXT && a.Data.StartsWith("v=spf1")).ToArray();
            aAnswersToCompare = aAnswersToCompare.Where(a => a.Type == DnsRecordType.TXT && a.Data.StartsWith("v=spf1")).ToArray();

            var sortedAAnswers = aAnswersPrimary.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();
            var sortedAAnswersCompared = aAnswersToCompare.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();

            // Check that the arrays have the same length
            Assert.True(sortedAAnswers.Length == sortedAAnswersCompared.Length);

            for (int i = 0; i < sortedAAnswers.Length; i++) {
                Assert.True((bool)(sortedAAnswers[i].Name == sortedAAnswersCompared[i].Name));
                Assert.True((bool)(sortedAAnswers[i].Type == sortedAAnswersCompared[i].Type));
                Assert.True((bool)(sortedAAnswers[i].Data.Length == sortedAAnswersCompared[i].Data.Length));
                Assert.True(Enumerable.SequenceEqual<char>(sortedAAnswers[i].Data, sortedAAnswersCompared[i].Data));
                Assert.True((bool)(sortedAAnswers[i].DataStrings.Length == sortedAAnswersCompared[i].DataStrings.Length));

                for (int j = 0; j < sortedAAnswers[i].DataStrings.Length; j++) {
                    Assert.True((bool)(sortedAAnswers[i].DataStrings[j] == sortedAAnswersCompared[i].DataStrings[j]));
                }
                for (int j = 0; j < sortedAAnswers[i].DataStringsEscaped.Length; j++) {
                    Assert.True((bool)(sortedAAnswers[i].DataStringsEscaped[j] == sortedAAnswersCompared[i].DataStringsEscaped[j]));
                }
            }
        }
    }

}
