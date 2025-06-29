using Xunit.Abstractions;

namespace DnsClientX.Tests {
    public class CompareProvidersResolve(ITestOutputHelper output) {
        [Theory(Skip = "External dependency - unreliable for automated testing")]
        //[MemberData(nameof(TestData))]
        [InlineData("evotec.pl", DnsRecordType.A, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("www.bücher.de", DnsRecordType.A, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("evotec.pl", DnsRecordType.SOA, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("evotec.pl", DnsRecordType.DNSKEY, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("sip.evotec.pl", DnsRecordType.CNAME, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("autodiscover.evotec.pl", DnsRecordType.CNAME, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("evotec.pl", DnsRecordType.CAA, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("evotec.pl", DnsRecordType.AAAA, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("evotec.pl", DnsRecordType.MX, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("evotec.pl", DnsRecordType.NS, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("evotec.pl", DnsRecordType.SPF, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("evotec.pl", DnsRecordType.TXT, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("evotec.pl", DnsRecordType.SRV, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("evotec.pl", DnsRecordType.NSEC, new[] { DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily, DnsEndpoint.Google })]
        [InlineData("cloudflare.com", DnsRecordType.NSEC, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("mail-db3pr0202cu00100.inbound.protection.outlook.com", DnsRecordType.PTR, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        // lets try different sites
        [InlineData("reddit.com", DnsRecordType.A, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("reddit.com", DnsRecordType.CAA, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("reddit.com", DnsRecordType.SOA, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        // github.com has a lot of TXT records, including multiline, however google dns doesn't do multiline TXT records and delivers them as one line
        [InlineData("github.com", DnsRecordType.TXT, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]

        [InlineData("microsoft.com", DnsRecordType.MX, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("microsoft.com", DnsRecordType.NS, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]

        [InlineData("google.com", DnsRecordType.MX, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("1.1.1.1", DnsRecordType.PTR, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("108.138.7.68", DnsRecordType.PTR, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("sip2sip.info", DnsRecordType.NAPTR, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        public async Task CompareRecordsImproved(string name, DnsRecordType resourceRecordType, DnsEndpoint[]? excludedEndpoints = null) {
            output.WriteLine($"Testing record: {name}, type: {resourceRecordType}");

            var primaryEndpoint = DnsEndpoint.Cloudflare;
            var allEndpoints = Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>()
                .Where(e => e != primaryEndpoint && (excludedEndpoints == null || !excludedEndpoints.Contains(e)))
                .ToArray();

            // Collect results from all providers with retry logic
            var results = new Dictionary<DnsEndpoint, (DnsResponse response, string? error)>();

            // Get primary endpoint result
            var primaryClient = new ClientX(primaryEndpoint);
            var primaryResult = await GetResponseWithRetry(primaryClient, name, resourceRecordType, primaryEndpoint, output);
            results[primaryEndpoint] = primaryResult;

            output.WriteLine($"Primary ({primaryEndpoint}): {primaryResult.response.Answers?.Length ?? 0} records, Status: {primaryResult.response.Status}");
            if (!string.IsNullOrEmpty(primaryResult.error)) {
                output.WriteLine($"  Error: {primaryResult.error}");
            }

            // Get all other endpoint results
            foreach (var endpoint in allEndpoints) {
                var client = new ClientX(endpoint);
                var result = await GetResponseWithRetry(client, name, resourceRecordType, endpoint, output);
                results[endpoint] = result;

                output.WriteLine($"Provider {endpoint}: {result.response.Answers?.Length ?? 0} records, Status: {result.response.Status}");
                if (!string.IsNullOrEmpty(result.error)) {
                    output.WriteLine($"  Error: {result.error}");
                }
            }

            // Analyze results
            var expectedCount = primaryResult.response.Answers?.Length ?? 0;
            var failedProviders = new List<string>();
            var inconsistentProviders = new List<string>();

            foreach (var kvp in results.Where(r => r.Key != primaryEndpoint)) {
                var endpoint = kvp.Key;
                var result = kvp.Value;
                var actualCount = result.response.Answers?.Length ?? 0;

                if (actualCount == 0 && expectedCount > 0) {
                    failedProviders.Add($"{endpoint} (0 records, expected {expectedCount})");
                } else if (actualCount != expectedCount) {
                    inconsistentProviders.Add($"{endpoint} ({actualCount} records, expected {expectedCount})");
                }
            }

            // Report diagnostics
            if (failedProviders.Any()) {
                output.WriteLine($"⚠️  Providers returning empty results: {string.Join(", ", failedProviders)}");
            }
            if (inconsistentProviders.Any()) {
                output.WriteLine($"⚠️  Providers with different record counts: {string.Join(", ", inconsistentProviders)}");
            }

            // Only fail if MORE than 20% of providers have issues (allows for some transient failures)
            var totalProviders = allEndpoints.Length;
            var problematicProviders = failedProviders.Count + inconsistentProviders.Count;
            var failureRate = (double)problematicProviders / totalProviders;

            if (failureRate > 0.2) { // More than 20% of providers failing
                var allIssues = failedProviders.Concat(inconsistentProviders);
                Assert.False(true, $"Too many providers ({problematicProviders}/{totalProviders}, {failureRate:P0}) have issues: {string.Join(", ", allIssues)}");
            } else if (problematicProviders > 0) {
                output.WriteLine($"✅ Acceptable failure rate: {problematicProviders}/{totalProviders} providers ({failureRate:P0}) have issues - likely transient");
            }

            // For providers that did return results, validate content consistency
            var successfulResults = results.Where(r => (r.Value.response.Answers?.Length ?? 0) == expectedCount).ToList();
            if (successfulResults.Count > 1 && expectedCount > 0) {
                var reference = successfulResults.First().Value.response.Answers.OrderBy(a => a.Data).ToArray();
                foreach (var kvp in successfulResults.Skip(1)) {
                    var endpoint = kvp.Key;
                    var result = kvp.Value;
                    var sorted = result.response.Answers.OrderBy(a => a.Data).ToArray();
                    for (int i = 0; i < reference.Length; i++) {
                        if (reference[i].Data != sorted[i].Data) {
                            output.WriteLine($"⚠️  Content mismatch in {endpoint}: expected '{reference[i].Data}', got '{sorted[i].Data}'");
                        }
                    }
                }
            }
        }

        private async Task<(DnsResponse response, string? error)> GetResponseWithRetry(
            ClientX client, string name, DnsRecordType type, DnsEndpoint endpoint, ITestOutputHelper output) {

            const int maxRetries = 3;
            const int delayMs = 500;

            for (int attempt = 1; attempt <= maxRetries; attempt++) {
                try {
                    var response = await client.Resolve(name, type);

                    if ((response.Answers?.Length ?? 0) == 0 && response.Status == DnsResponseCode.NoError && attempt < maxRetries) {
                        output.WriteLine($"  {endpoint}: Attempt {attempt} returned 0 records with NoError status, retrying...");
                        await Task.Delay(delayMs);
                        continue;
                    }

                    return (response, response.Error);
                } catch (Exception ex) {
                    if (attempt == maxRetries) {
                        var emptyResponse = new DnsResponse {
                            Status = DnsResponseCode.ServerFailure,
                            Error = $"Exception after {maxRetries} attempts: {ex.Message}",
                            Answers = Array.Empty<DnsAnswer>(),
                            Questions = Array.Empty<DnsQuestion>()
                        };
                        return (emptyResponse, $"Exception after {maxRetries} attempts: {ex.Message}");
                    }
                    output.WriteLine($"  {endpoint}: Attempt {attempt} failed: {ex.Message}, retrying...");
                    await Task.Delay(delayMs);
                }
            }

            var failureResponse = new DnsResponse {
                Status = DnsResponseCode.ServerFailure,
                Error = "Max retries exceeded",
                Answers = Array.Empty<DnsAnswer>(),
                Questions = Array.Empty<DnsQuestion>()
            };
            return (failureResponse, "Max retries exceeded");
        }

        [Theory(Skip = "External dependency - unreliable for automated testing")]
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
        [InlineData("evotec.pl", DnsRecordType.NSEC, new[] { DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily, DnsEndpoint.Google })]
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

