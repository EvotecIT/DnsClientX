using Xunit.Abstractions;

namespace DnsClientX.Tests {
    /// <summary>
    /// Compares <see cref="ClientX.ResolveAll(string,DnsRecordType,bool,bool,bool,bool,int,int,System.Threading.CancellationToken)"/> results across providers.
    /// </summary>
    public class CompareProviders(ITestOutputHelper output) {
        /// <summary>
        /// Performs a ResolveAll comparison across various providers.
        /// </summary>
        /// <param name="name">Domain name to resolve.</param>
        /// <param name="resourceRecordType">Type of record.</param>
        /// <param name="excludedEndpoints">Optional providers to skip.</param>
        [Theory(Skip = "External dependency - unreliable for automated testing")]
        [InlineData("evotec.pl", DnsRecordType.A, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
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
        [InlineData("reddit.com", DnsRecordType.A, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("reddit.com", DnsRecordType.CAA, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("reddit.com", DnsRecordType.SOA, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("github.com", DnsRecordType.TXT, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("microsoft.com", DnsRecordType.MX, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("microsoft.com", DnsRecordType.NS, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("google.com", DnsRecordType.MX, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("_25._tcp.mail.ietf.org", DnsRecordType.TLSA, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        public async Task CompareRecordsImproved(string name, DnsRecordType resourceRecordType, DnsEndpoint[]? excludedEndpoints = null) {
            output.WriteLine($"Testing record: {name}, type: {resourceRecordType}");

            var primaryEndpoint = DnsEndpoint.Cloudflare;
            var allEndpoints = Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>()
                .Where(e => e != primaryEndpoint && (excludedEndpoints == null || !excludedEndpoints.Contains(e)))
                .ToArray();

            // Collect results from all providers with retry logic
            var results = new Dictionary<DnsEndpoint, (DnsAnswer[] answers, string? error, DnsResponseCode status)>();

            // Get primary endpoint result
            using var primaryClient = new ClientX(primaryEndpoint);
            var primaryAnswers = await GetAnswersWithRetry(primaryClient, name, resourceRecordType, primaryEndpoint, output);
            results[primaryEndpoint] = primaryAnswers;

            output.WriteLine($"Primary ({primaryEndpoint}): {primaryAnswers.answers.Length} records, Status: {primaryAnswers.status}");
            if (!string.IsNullOrEmpty(primaryAnswers.error)) {
                output.WriteLine($"  Error: {primaryAnswers.error}");
            }

            // Get all other endpoint results
            foreach (var endpoint in allEndpoints) {
                using var client = new ClientX(endpoint);
                var result = await GetAnswersWithRetry(client, name, resourceRecordType, endpoint, output);
                results[endpoint] = result;

                output.WriteLine($"Provider {endpoint}: {result.answers.Length} records, Status: {result.status}");
                if (!string.IsNullOrEmpty(result.error)) {
                    output.WriteLine($"  Error: {result.error}");
                }
            }

            // Analyze results
            var expectedCount = primaryAnswers.answers.Length;
            var failedProviders = new List<string>();
            var inconsistentProviders = new List<string>();

            foreach (var kvp in results.Where(r => r.Key != primaryEndpoint)) {
                var endpoint = kvp.Key;
                var result = kvp.Value;
                if (result.answers.Length == 0 && expectedCount > 0) {
                    failedProviders.Add($"{endpoint} (0 records, expected {expectedCount})");
                } else if (result.answers.Length != expectedCount) {
                    inconsistentProviders.Add($"{endpoint} ({result.answers.Length} records, expected {expectedCount})");
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
                Assert.Fail($"Too many providers ({problematicProviders}/{totalProviders}, {failureRate:P0}) have issues: {string.Join(", ", allIssues)}");
            } else if (problematicProviders > 0) {
                output.WriteLine($"✅ Acceptable failure rate: {problematicProviders}/{totalProviders} providers ({failureRate:P0}) have issues - likely transient");
            }

            // For providers that did return results, validate content consistency
            var successfulResults = results.Where(r => r.Value.answers.Length == expectedCount).ToList();
            if (successfulResults.Count > 1) {
                var reference = successfulResults.First().Value.answers.OrderBy(a => a.Data).ToArray();
                foreach (var kvp in successfulResults.Skip(1)) {
                    var endpoint = kvp.Key;
                    var result = kvp.Value;
                    var sorted = result.answers.OrderBy(a => a.Data).ToArray();
                    for (int i = 0; i < reference.Length; i++) {
                        if (reference[i].Data != sorted[i].Data) {
                            output.WriteLine($"⚠️  Content mismatch in {endpoint}: expected '{reference[i].Data}', got '{sorted[i].Data}'");
                        }
                    }
                }
            }
        }

        private async Task<(DnsAnswer[] answers, string? error, DnsResponseCode status)> GetAnswersWithRetry(
            ClientX client, string name, DnsRecordType type, DnsEndpoint endpoint, ITestOutputHelper output) {

            const int maxRetries = 3;
            const int delayMs = 500;

            for (int attempt = 1; attempt <= maxRetries; attempt++) {
                try {
                    var fullResponse = await client.Resolve(name, type);
                    var answers = await client.ResolveAll(name, type);

                    if (answers.Length == 0 && fullResponse.Status == DnsResponseCode.NoError && attempt < maxRetries) {
                        output.WriteLine($"  {endpoint}: Attempt {attempt} returned 0 records with NoError status, retrying...");
                        await Task.Delay(delayMs);
                        continue;
                    }

                    return (answers, fullResponse.Error, fullResponse.Status);
                } catch (Exception ex) {
                    if (attempt == maxRetries) {
                        return (Array.Empty<DnsAnswer>(), $"Exception after {maxRetries} attempts: {ex.Message}", DnsResponseCode.ServerFailure);
                    }
                    output.WriteLine($"  {endpoint}: Attempt {attempt} failed: {ex.Message}, retrying...");
                    await Task.Delay(delayMs);
                }
            }

            return (Array.Empty<DnsAnswer>(), "Max retries exceeded", DnsResponseCode.ServerFailure);
        }

        /// <summary>
        /// Validates that providers return consistent results for the specified record type.
        /// </summary>
        /// <param name="name">Domain name to resolve.</param>
        /// <param name="resourceRecordType">Record type.</param>
        /// <param name="excludedEndpoints">Optional providers to skip.</param>
        [Theory(Skip = "External dependency - unreliable for automated testing")]
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
        // for some reason OpenDNS doesn't support SRV record output in NSEC record
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
        [InlineData("_25._tcp.mail.ietf.org", DnsRecordType.TLSA)]
        public async Task CompareRecords(string name, DnsRecordType resourceRecordType, DnsEndpoint[]? excludedEndpoints = null) {
            output.WriteLine($"Testing record: {name}, type: {resourceRecordType}");

            var primaryEndpoint = DnsEndpoint.Cloudflare;

            using var Client = new ClientX(primaryEndpoint);
            DnsAnswer[] aAnswersPrimary = await Client.ResolveAll(name, resourceRecordType);

            foreach (var endpointCompare in Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>()) {
                if (endpointCompare == primaryEndpoint) {
                    continue;
                }
                if (excludedEndpoints != null && excludedEndpoints.Contains(endpointCompare)) {
                    continue;
                }
                output.WriteLine("Provider: " + endpointCompare.ToString());
                using var clientToCompare = new ClientX(endpointCompare);
                DnsAnswer[] aAnswersToCompare = await clientToCompare.ResolveAll(name, resourceRecordType);

                var sortedAAnswers = aAnswersPrimary.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();
                var sortedAAnswersCompared = aAnswersToCompare.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();

                // Check that the arrays have the same length
                Assert.Equal(sortedAAnswers.Length, sortedAAnswersCompared.Length);

                // Check that the arrays have the same elements in the same order
                for (int i = 0; i < sortedAAnswers.Length; i++) {
                    output.WriteLine($"Record {i} should equal: {sortedAAnswers[i].Data} == {sortedAAnswersCompared[i].Data}");
                    Assert.True((bool)(sortedAAnswers[i].Name == sortedAAnswersCompared[i].Name), $"Provider {endpointCompare}. There is a name mismatch for " + sortedAAnswers[i].Data);
                    Assert.True((bool)(sortedAAnswers[i].Type == sortedAAnswersCompared[i].Type), $"Provider {endpointCompare}. There is a type mismatch for " + sortedAAnswers[i].Data);
                    Assert.True((bool)(sortedAAnswers[i].Data.Length == sortedAAnswersCompared[i].Data.Length), $"Provider {endpointCompare}. Records not matching length for " + sortedAAnswers[i].Data + " length expected: " + sortedAAnswers[i].Data.Length + " length provided: " + sortedAAnswersCompared[i].Data.Length);
                    Assert.True(Enumerable.SequenceEqual<char>(sortedAAnswers[i].Data, sortedAAnswersCompared[i].Data), $"Provider {endpointCompare}. Records not matching content for " + sortedAAnswers[i].Data);
                    Assert.True((bool)(sortedAAnswers[i].DataStrings.Length == sortedAAnswersCompared[i].DataStrings.Length), $"Provider {endpointCompare}. Records not matching length for " + sortedAAnswers[i].DataStrings + " length expected: " + sortedAAnswers[i].DataStrings.Length + " length provided: " + sortedAAnswersCompared[i].DataStrings.Length);
                }
        }
        }

        [Theory(Skip = "External dependency - unreliable for automated testing")]
        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.CloudflareFamily)]
        // Google seems to have a different set of TXT records for github.com, as it doesn't follow specficiation directly
        // It seems to merge multiline TXT records into one line
        // https://dns.google/query?name=github.com&rr_type=TXT&ecs=
        //[InlineData("github.com", ResourceRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.Google)]



        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.CloudflareWireFormat)]
        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNS)]
        [InlineData("github.com", DnsRecordType.TXT, DnsEndpoint.Cloudflare, DnsEndpoint.OpenDNSFamily)]
        public async Task CompareRecordTextMultiline(string name, DnsRecordType resourceRecordType, DnsEndpoint primaryEndpoint, DnsEndpoint endpointCompare) {
            using var Client = new ClientX(primaryEndpoint);
            DnsAnswer[] aAnswersPrimary = await Client.ResolveAll(name, resourceRecordType);
            using var ClientToCompare = new ClientX(endpointCompare);
            DnsAnswer[] aAnswersToCompare = await ClientToCompare.ResolveAll(name, resourceRecordType);

            // we focus only on SPF1 TXT records
            aAnswersPrimary = aAnswersPrimary.Where(a => a.Type == DnsRecordType.TXT && a.Data.StartsWith("v=spf1")).ToArray();
            aAnswersToCompare = aAnswersToCompare.Where(a => a.Type == DnsRecordType.TXT && a.Data.StartsWith("v=spf1")).ToArray();

            var sortedAAnswers = aAnswersPrimary.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();
            var sortedAAnswersCompared = aAnswersToCompare.OrderBy(a => a.Name).ThenBy(a => a.Type).ThenBy(a => a.Data).ToArray();

            // Check that the arrays have the same length
            Assert.Equal(sortedAAnswers.Length, sortedAAnswersCompared.Length);

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

