using System;
using System.Linq;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Protects resolver configuration identity across benchmark execution and reports.</summary>
public sealed class ResolverBenchmarkIdentityTests {
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
