using System;
using System.Linq;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Protects resolver configuration identity across benchmark execution and reports.</summary>
public sealed class ResolverBenchmarkIdentityTests {
    /// <summary>Public report builders preserve opaque ID case, normalize surrounding whitespace and correlate answer evidence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallerCandidateIdsRemainDistinctAndNormalized(bool aggregateInput) {
        var attempts = new[] { " Resolver:A ", "resolver:a" }.Select((id, index) => new ResolverQueryAttemptResult {
            Target = "same", CandidateId = id, Name = "example.com", RecordType = DnsRecordType.A,
            Elapsed = TimeSpan.FromMilliseconds(index + 1), Response = new DnsResponse {
                Status = DnsResponseCode.NoError, Answers = new[] { new DnsAnswer {
                    Name = "example.com", Type = DnsRecordType.A, DataRaw = "192.0.2." + (index + 1)
                } }
            }
        }).ToArray();
        ResolverBenchmarkReport report = aggregateInput
            ? ResolverBenchmarkReportBuilder.Build(attempts.Select(a => new ResolverBenchmarkCandidate {
                Target = a.Target, CandidateId = a.CandidateId, TotalQueries = 1, SuccessCount = 1, AverageMs = a.Elapsed.TotalMilliseconds
            }).ToArray(), new[] { "example.com" }, new[] { DnsRecordType.A }, 1, 1, 2000, new ResolverBenchmarkPolicy())
            : ResolverBenchmarkReportBuilder.Build(attempts, new[] { "example.com" }, new[] { DnsRecordType.A }, 1, 1, 2000, new ResolverBenchmarkPolicy());
        Assert.Equal(2, report.Results.Length);
        Assert.Equal(new[] { "Resolver:A", "resolver:a" }, report.Results.Select(row => row.CandidateId));
        Assert.All(report.Results, row => Assert.Equal(1, row.TotalQueries));
        if (!aggregateInput) {
            ResolverScoreComparison comparison = ResolverScoreComparer.Compare(report.Snapshot, report.Snapshot);
            Assert.Equal(2, comparison.Candidates.Length);
            Assert.Equal(2, comparison.Answers.Length);
            Assert.All(comparison.Candidates, row => Assert.Equal("Unchanged", row.Change));
            Assert.All(comparison.Answers, row => Assert.Single(row.Current));
        }
    }

    /// <summary>Two valid endpoint configurations sharing a label retain their individual outcomes.</summary>
    [Fact]
    public async Task SameLabelEndpointVariantsRemainSeparateCandidates() {
        var endpoints = new[] {
            new DnsResolverEndpoint { Host = "127.0.0.1", Port = 53535, Transport = Transport.Udp, Timeout = TimeSpan.FromSeconds(1) },
            new DnsResolverEndpoint { Host = "127.0.0.1", Port = 53535, Transport = Transport.Udp, Timeout = TimeSpan.FromSeconds(2) }
        };
        ResolverExecutionTarget[] targets = ResolverExecutionPlanBuilder.BuildExplicitTargets(endpoints);
        Assert.Equal(2, targets.Length);
        Assert.Equal(targets[0].DisplayName, targets[1].DisplayName);

        ResolverBenchmarkReport report = await ResolverBenchmarkWorkflow.RunAsync(
            targets, new[] { "example.com" }, new[] { DnsRecordType.A }, 1, 2, 3000,
            new ResolverQueryRunOptions(), new ResolverBenchmarkPolicy { MinSuccessfulCandidates = 2 },
            explicitOverride: (endpoint, name, type, token) => Task.FromResult(new ResolverQueryAttemptResult {
                Target = ResolverExecutionPlanBuilder.DescribeEndpoint(endpoint),
                Resolver = "127.0.0.1:53535", RequestFormat = DnsRequestFormat.DnsOverUDP,
                Elapsed = TimeSpan.FromMilliseconds(endpoint.Timeout!.Value.TotalSeconds),
                Response = new DnsResponse { Status = DnsResponseCode.NoError, Answers = new[] {
                    new DnsAnswer { Name = name, Type = type, DataRaw = "192.0.2." + endpoint.Timeout.Value.TotalSeconds }
                } }
            }));

        Assert.Equal(2, report.Results.Length);
        Assert.All(report.Results, row => Assert.Equal(1, row.TotalQueries));
        Assert.Equal(2, report.Summary.CandidateCount);
        Assert.True(report.Evaluation.PolicyPassed);
        Assert.Equal(2, report.Snapshot.Results.Length);
        Assert.Equal(2, report.Snapshot.Answers.Select(row => row.CandidateId).Distinct().Count());
        Assert.Equal(targets[0].CandidateId, report.Summary.RecommendedCandidateId);
        string summaryLine = ResolverReportTextFormatter.BuildBenchmarkSummaryLine(report.Summary, 0);
        Assert.Contains("best_candidate_id=" + Uri.EscapeDataString(targets[0].CandidateId) + " ", summaryLine);
        Assert.All(report.Results, row => Assert.Contains(row.CandidateId,
            ResolverReportTextFormatter.BuildBenchmarkResultLines(row, duration => duration.TotalMilliseconds + " ms")[0]));
        Assert.True(ResolverScoreSelector.TrySelectRecommended(report.Snapshot, out var selected, out string? error), error);
        Assert.Equal(TimeSpan.FromSeconds(1), selected!.ExplicitEndpoint!.Timeout);

        // Readable labels and catalog ordering do not change the configured identity.
        ResolverExecutionTarget[] reordered = ResolverExecutionPlanBuilder.BuildExplicitTargets(Enumerable.Reverse(endpoints).ToArray());
        Assert.Equal(targets[0].CandidateId, reordered[1].CandidateId);
        ResolverScoreSnapshot renamed = DnsClientXJsonSerializer.Deserialize<ResolverScoreSnapshot>(
            DnsClientXJsonSerializer.Serialize(report.Snapshot))!;
        foreach (var row in renamed.Results) row.Target = "renamed " + row.Target;
        foreach (var row in renamed.Answers) row.Target = "renamed " + row.Target;
        ResolverScoreComparison comparison = ResolverScoreComparer.Compare(report.Snapshot, renamed);
        Assert.True(comparison.ScopeComparable, string.Join("; ", comparison.IncomparableReasons));
        Assert.Equal(2, comparison.Answers.Length);
        Assert.All(comparison.Answers, row => Assert.False(row.Changed));
    }
}
