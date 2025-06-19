using Xunit.Abstractions;

using System.Threading.Tasks;
using System.Collections.Generic;

namespace DnsClientX.Tests {
    public class CompareProvidersResolveFilter(ITestOutputHelper output) {
        [Theory]
        [InlineData("evotec.pl", DnsRecordType.TXT)]
        [InlineData("microsoft.com", DnsRecordType.TXT)]
        [InlineData("disneyplus.com", DnsRecordType.TXT)]
        [InlineData("github.com", DnsRecordType.TXT)]
        public async Task CompareRecords(string name, DnsRecordType resourceRecordType, DnsEndpoint[]? excludedEndpoints = null) {
            output.WriteLine($"Testing record: {name}, type: {resourceRecordType}");

            string filter = "v=spf1";

            var primaryEndpoint = DnsEndpoint.Cloudflare;

            var Client = new ClientX(primaryEndpoint);
            DnsResponse aAnswersPrimary = await Client.ResolveFilter(name, resourceRecordType, filter);

            foreach (var endpointCompare in Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>()) {
                if (endpointCompare == primaryEndpoint) {
                    continue;
                }

                if (excludedEndpoints != null && excludedEndpoints.Contains(endpointCompare)) {
                    continue;
                }

                output.WriteLine("Provider: " + endpointCompare.ToString());

                var ClientToCompare = new ClientX(endpointCompare);
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

                var sortedQuestions = aAnswersPrimary.Questions.OrderBy(a => a.Name).ThenBy(a => a.Type)
                    .ThenBy(a => a.Type).ToArray();
                var sortedQuestionsCompared = aAnswersToCompare.Questions.OrderBy(a => a.Name).ThenBy(a => a.Type)
                    .ThenBy(a => a.Type).ToArray();

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

        [Theory]
        [InlineData(new[] { "evotec.pl", "microsoft.com", "disneyplus.com" }, DnsRecordType.TXT)]
        public async Task CompareRecordsMulti(string[] names, DnsRecordType resourceRecordType, DnsEndpoint[]? excludedEndpoints = null) {
            string filter = "v=spf1";
            var primaryEndpoint = DnsEndpoint.Cloudflare;
            var Client = new ClientX(primaryEndpoint);

            DnsResponse[] aAnswersPrimary = await Client.ResolveFilter(names, resourceRecordType, filter);

            foreach (var endpointCompare in Enum.GetValues(typeof(DnsEndpoint)).Cast<DnsEndpoint>()) {
                if (endpointCompare == primaryEndpoint) {
                    continue;
                }

                if (excludedEndpoints != null && excludedEndpoints.Contains(endpointCompare)) {
                    continue;
                }

                var ClientToCompare = new ClientX(endpointCompare);
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
