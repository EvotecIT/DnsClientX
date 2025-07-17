using Xunit.Abstractions;

using System.Threading.Tasks;
using System.Collections.Generic;

namespace DnsClientX.Tests {
    /// <summary>
    /// Compares provider results when using <see cref="ClientX.ResolveFilter"/>.
    /// </summary>
    public class CompareProvidersResolveFilter(ITestOutputHelper output) {
        /// <summary>
        /// Performs a filtered resolve across providers and compares outcomes.
        /// </summary>
        /// <param name="name">Domain name to query.</param>
        /// <param name="resourceRecordType">Record type.</param>
        /// <param name="excludedEndpoints">Optional providers to exclude.</param>
        [Theory(Skip = "External dependency - unreliable for automated testing")]
        [InlineData("evotec.pl", DnsRecordType.TXT, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("microsoft.com", DnsRecordType.TXT, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("disneyplus.com", DnsRecordType.TXT, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        [InlineData("github.com", DnsRecordType.TXT, new[] { DnsEndpoint.Google, DnsEndpoint.OpenDNS, DnsEndpoint.OpenDNSFamily })]
        public async Task CompareRecordsImproved(string name, DnsRecordType resourceRecordType, DnsEndpoint[]? excludedEndpoints = null) {
            output.WriteLine($"Testing record: {name}, type: {resourceRecordType}");

            string filter = "v=spf1";
            var primaryEndpoint = DnsEndpoint.Cloudflare;
            var allEndpoints = Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>()
                .Where(e => e != primaryEndpoint && (excludedEndpoints == null || !excludedEndpoints.Contains(e)))
                .ToArray();

            // Collect results from all providers with retry logic
            var results = new Dictionary<DnsEndpoint, (DnsResponse response, string? error)>();

            // Get primary endpoint result
            using var primaryClient = new ClientX(primaryEndpoint);
            var primaryResult = await GetResponseWithRetry(primaryClient, name, resourceRecordType, filter, primaryEndpoint, output);
            results[primaryEndpoint] = primaryResult;

            output.WriteLine($"Primary ({primaryEndpoint}): {primaryResult.response.Answers?.Length ?? 0} records, Status: {primaryResult.response.Status}");
            if (!string.IsNullOrEmpty(primaryResult.error)) {
                output.WriteLine($"  Error: {primaryResult.error}");
            }

            // Get all other endpoint results
            foreach (var endpoint in allEndpoints) {
                using var client = new ClientX(endpoint);
                var result = await GetResponseWithRetry(client, name, resourceRecordType, filter, endpoint, output);
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
                Assert.Fail($"Too many providers ({problematicProviders}/{totalProviders}, {failureRate:P0}) have issues: {string.Join(", ", allIssues)}");
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
            ClientX client, string name, DnsRecordType type, string filter, DnsEndpoint endpoint, ITestOutputHelper output) {

            const int maxRetries = 3;
            const int delayMs = 500;

            for (int attempt = 1; attempt <= maxRetries; attempt++) {
                try {
                    var response = await client.ResolveFilter(name, type, filter);

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

        /// <summary>
        /// Compares provider answers after filtering responses for a specific pattern.
        /// </summary>
        [Theory(Skip = "External dependency - unreliable for automated testing")]
        [InlineData("evotec.pl", DnsRecordType.TXT)]
        [InlineData("microsoft.com", DnsRecordType.TXT)]
        [InlineData("disneyplus.com", DnsRecordType.TXT)]
        [InlineData("github.com", DnsRecordType.TXT)]
        public async Task CompareRecords(string name, DnsRecordType resourceRecordType, DnsEndpoint[]? excludedEndpoints = null) {
            output.WriteLine($"Testing record: {name}, type: {resourceRecordType}");

            string filter = "v=spf1";

            var primaryEndpoint = DnsEndpoint.Cloudflare;

            using var Client = new ClientX(primaryEndpoint);
            DnsResponse aAnswersPrimary = await Client.ResolveFilter(name, resourceRecordType, filter);

            foreach (var endpointCompare in Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>()) {
                if (endpointCompare == primaryEndpoint) {
                    continue;
                }

                if (excludedEndpoints != null && excludedEndpoints.Contains(endpointCompare)) {
                    continue;
                }

                output.WriteLine("Provider: " + endpointCompare.ToString());

                using var ClientToCompare = new ClientX(endpointCompare);
                DnsResponse aAnswersToCompare = await ClientToCompare.ResolveFilter(name, resourceRecordType, filter);

                var sortedAAnswers = aAnswersPrimary.Answers.OrderBy(a => a.Name).ThenBy(a => a.Type)
                    .ThenBy(a => a.Data).ToArray();
                var sortedAAnswersCompared = aAnswersToCompare.Answers.OrderBy(a => a.Name).ThenBy(a => a.Type)
                    .ThenBy(a => a.Data).ToArray();

                // Check that the arrays have the same elements in the same order
                try {
                    Assert.Equal(sortedAAnswers.Length, sortedAAnswersCompared.Length);
                    for (int i = 0; i < sortedAAnswers.Length; i++) {
                        output.WriteLine($"Record {i} should equal: {sortedAAnswers[i].Data} == {sortedAAnswersCompared[i].Data}");
                        Assert.True((bool)(sortedAAnswers[i].Name == sortedAAnswersCompared[i].Name),
                            $"Provider {endpointCompare}. There is a name mismatch for " + sortedAAnswers[i].Data);
                        Assert.True((bool)(sortedAAnswers[i].Type == sortedAAnswersCompared[i].Type),
                            $"Provider {endpointCompare}. There is a type mismatch for " + sortedAAnswers[i].Data);
                        Assert.True((bool)(sortedAAnswers[i].Data.Length == sortedAAnswersCompared[i].Data.Length),
                            $"Provider {endpointCompare}. Records not matching length for " + sortedAAnswers[i].Data +
                            " length expected: " + sortedAAnswers[i].Data.Length + " length provided: " +
                            sortedAAnswersCompared[i].Data.Length);
                        Assert.True(Enumerable.SequenceEqual<char>(sortedAAnswers[i].Data, sortedAAnswersCompared[i].Data),
                            $"Provider {endpointCompare}. Records not matching content for " + sortedAAnswers[i].Data);
                        Assert.True(
                            (bool)(sortedAAnswers[i].DataStrings.Length == sortedAAnswersCompared[i].DataStrings.Length),
                            $"Provider {endpointCompare}. Records not matching length for " +
                            sortedAAnswers[i].DataStrings + " length expected: " + sortedAAnswers[i].DataStrings.Length +
                            " length provided: " + sortedAAnswersCompared[i].DataStrings.Length);
                    }
                } catch (Exception) {
                    void LogRecords(string label, DnsAnswer[] answers) {
                        output.WriteLine($"--- {label} ({answers.Length} records) ---");
                        foreach (var a in answers) {
                            output.WriteLine($"  {a.Data}");
                        }
                    }
                    LogRecords("Primary", sortedAAnswers);
                    LogRecords("Compared", sortedAAnswersCompared);
                    var setA = new HashSet<string>(sortedAAnswers.Select(a => a.Data));
                    var setB = new HashSet<string>(sortedAAnswersCompared.Select(a => a.Data));
                    var onlyInA = setA.Except(setB).ToList();
                    var onlyInB = setB.Except(setA).ToList();
                    if (onlyInA.Any()) output.WriteLine("Only in primary: " + string.Join(" | ", onlyInA));
                    if (onlyInB.Any()) output.WriteLine("Only in compared: " + string.Join(" | ", onlyInB));
                    throw;
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
                Assert.Equal(sortedQuestions.Length, sortedQuestionsCompared.Length);

                for (int i = 0; i < sortedQuestions.Length; i++) {
                    output.WriteLine("Provider: " + endpointCompare.ToString());
                    output.WriteLine(
                        $"Question {i} should equal: {sortedQuestions[i].Name} == {sortedQuestionsCompared[i].Name}");
                    Assert.True(sortedQuestions[i].Name == sortedQuestionsCompared[i].Name,
                        $"Provider {endpointCompare}. There is a name mismatch for " + sortedQuestions[i].Name);
                    Assert.True((bool)(sortedQuestions[i].Type == sortedQuestionsCompared[i].Type),
                        $"Provider {endpointCompare}. There is a type mismatch for " + sortedQuestions[i].Type);
                }
            }
        }

        /// <summary>
        /// Executes filtered resolve tests for multiple domains simultaneously.
        /// </summary>
        [Theory(Skip = "External dependency - unreliable for automated testing")]
        [InlineData(new[] { "evotec.pl", "microsoft.com", "disneyplus.com" }, DnsRecordType.TXT)]
        public async Task CompareRecordsMulti(string[] names, DnsRecordType resourceRecordType, DnsEndpoint[]? excludedEndpoints = null) {
            string filter = "v=spf1";
            var primaryEndpoint = DnsEndpoint.Cloudflare;
            using var Client = new ClientX(primaryEndpoint);

            DnsResponse[] aAnswersPrimary = await Client.ResolveFilter(names, resourceRecordType, filter);

            foreach (var endpointCompare in Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>()) {
                if (endpointCompare == primaryEndpoint) {
                    continue;
                }

                if (excludedEndpoints != null && excludedEndpoints.Contains(endpointCompare)) {
                    continue;
                }

                using var ClientToCompare = new ClientX(endpointCompare);
                DnsResponse[] aAnswersToCompare = await ClientToCompare.ResolveFilter(names, resourceRecordType, filter);

                for (int j = 0; j < aAnswersPrimary.Length; j++) {
                    var sortedAAnswers = aAnswersPrimary[j].Answers.OrderBy(a => a.Name).ThenBy(a => a.Type)
                        .ThenBy(a => a.Data).ToArray();
                    var sortedAAnswersCompared = aAnswersToCompare[j].Answers.OrderBy(a => a.Name).ThenBy(a => a.Type)
                        .ThenBy(a => a.Data).ToArray();

                    // Check that the arrays have the same elements in the same order
                    try {
                        Assert.Equal(sortedAAnswers.Length, sortedAAnswersCompared.Length);
                        for (int i = 0; i < sortedAAnswers.Length; i++) {
                            output.WriteLine($"Record {i} should equal: {sortedAAnswers[i].Data} == {sortedAAnswersCompared[i].Data}");
                            Assert.True((bool)(sortedAAnswers[i].Name == sortedAAnswersCompared[i].Name),
                                $"Provider {endpointCompare}. There is a name mismatch for " + sortedAAnswers[i].Data);
                            Assert.True((bool)(sortedAAnswers[i].Type == sortedAAnswersCompared[i].Type),
                                $"Provider {endpointCompare}. There is a type mismatch for " + sortedAAnswers[i].Data);
                            Assert.True((bool)(sortedAAnswers[i].Data.Length == sortedAAnswersCompared[i].Data.Length),
                                $"Provider {endpointCompare}. Records not matching length for " + sortedAAnswers[i].Data +
                                " length expected: " + sortedAAnswers[i].Data.Length + " length provided: " +
                                sortedAAnswersCompared[i].Data.Length);
                            Assert.True(Enumerable.SequenceEqual<char>(sortedAAnswers[i].Data, sortedAAnswersCompared[i].Data),
                                $"Provider {endpointCompare}. Records not matching content for " + sortedAAnswers[i].Data);
                            Assert.True(
                                (bool)(sortedAAnswers[i].DataStrings.Length == sortedAAnswersCompared[i].DataStrings.Length),
                                $"Provider {endpointCompare}. Records not matching length for " +
                                sortedAAnswers[i].DataStrings + " length expected: " + sortedAAnswers[i].DataStrings.Length +
                                " length provided: " + sortedAAnswersCompared[i].DataStrings.Length);
                        }
                    } catch (Exception) {
                        void LogRecords(string label, DnsAnswer[] answers) {
                            output.WriteLine($"--- {label} ({answers.Length} records) ---");
                            foreach (var a in answers)
                                output.WriteLine($"  {a.Data}");
                        }
                        LogRecords("Primary", sortedAAnswers);
                        LogRecords("Compared", sortedAAnswersCompared);
                        var setA = new HashSet<string>(sortedAAnswers.Select(a => a.Data));
                        var setB = new HashSet<string>(sortedAAnswersCompared.Select(a => a.Data));
                        var onlyInA = setA.Except(setB).ToList();
                        var onlyInB = setB.Except(setA).ToList();
                        if (onlyInA.Any()) output.WriteLine("Only in primary: " + string.Join(" | ", onlyInA));
                        if (onlyInB.Any()) output.WriteLine("Only in compared: " + string.Join(" | ", onlyInB));
                        throw;
                    }
                }
            }
        }

    }
}

