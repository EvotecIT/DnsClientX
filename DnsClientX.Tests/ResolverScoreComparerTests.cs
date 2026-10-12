using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX.Tests;

/// <summary>Protects factual change reporting and the distinction between evidence and authoritative truth.</summary>
public sealed class ResolverScoreComparerTests {
    private static DnsResponse Answer(string value, bool authoritative = false) => new DnsResponse { Status = DnsResponseCode.NoError, IsAuthoritativeAnswer = authoritative, Answers = new[] { new DnsAnswer { Name = "example.com", Type = DnsRecordType.A, DataRaw = value, TTL = 60 } } };
    private static ResolverScoreSnapshot Snapshot(bool passed, string value) => new ResolverScoreSnapshot {
        Context = new ResolverScoreRunContext { Host = "host", Runtime = "runtime", OperatingSystem = "os", Architecture = "arch", TargetExecutionKeys = new Dictionary<string, string> { ["System"] = "builtin:System" } },
        Summary = new ResolverScoreSummary { Domains = new[] { "example.com" }, RecordTypes = new[] { DnsRecordType.A }, TimeoutMs = 2000, PolicyPassed = passed },
        Results = new[] { new ResolverScoreEntry { Target = "System", SuccessCount = 1, SuccessPercent = 100, AverageMs = 10 } },
        Answers = new[] { new ResolverAnswerObservation { Target = "System", Name = "example.com", RecordType = DnsRecordType.A, Status = "NoError", AnswerSignature = DnsResponseAnswerSignature.Build(Answer(value)) } }
    };

    /// <summary>Only matching measurement scopes can produce a regression conclusion.</summary>
    [Fact]
    public void Compare_GatesHealthAndLatencyOnScope() {
        ResolverScoreSnapshot before = Snapshot(true, "192.0.2.1"), after = Snapshot(false, "192.0.2.2");
        after.Results[0].AverageMs = 15;
        ResolverScoreComparison comparable = ResolverScoreComparer.Compare(before, after);
        Assert.True(comparable.ScopeComparable); Assert.True(comparable.HealthRegression);
        Assert.Equal(5d, comparable.Candidates[0].AverageMsDelta);
        after.Context!.Host = "another-host";
        ResolverScoreComparison differentHost = ResolverScoreComparer.Compare(before, after);
        Assert.False(differentHost.ScopeComparable); Assert.False(differentHost.HealthRegression);
        Assert.Null(differentHost.Candidates[0].AverageMsDelta);
        after.Context = null;
        after.Answers = Array.Empty<ResolverAnswerObservation>();
        ResolverScoreComparison legacy = ResolverScoreComparer.Compare(before, after);
        Assert.False(legacy.ScopeComparable); Assert.Null(Assert.Single(legacy.Answers).Changed);
    }

    /// <summary>Reports additions/removals and reference divergence without deriving correctness from a majority.</summary>
    [Fact]
    public void Compare_ReportsChangesAndCallerSuppliedAuthoritativeContext() {
        ResolverScoreSnapshot before = Snapshot(true, "192.0.2.1"), after = Snapshot(true, "192.0.2.2");
        before.Results = before.Results.Concat(new[] { new ResolverScoreEntry { Target = "Google" } }).ToArray();
        after.Results = after.Results.Concat(new[] { new ResolverScoreEntry { Target = "Cloudflare" } }).ToArray();
        before.Context!.TargetExecutionKeys!["Google"] = "builtin:Google";
        after.Context!.TargetExecutionKeys!["Cloudflare"] = "builtin:Cloudflare";
        ResolverScoreComparison comparison = ResolverScoreComparer.Compare(before, after, new[] { new ResolverAnswerReference { Name = "example.com.", RecordType = DnsRecordType.A, Source = "operator capture", Response = Answer("192.0.2.3", authoritative: true) } });
        Assert.Contains(comparison.Candidates, c => c.Target == "Google" && c.Change == "Removed");
        Assert.Contains(comparison.Candidates, c => c.Target == "Cloudflare" && c.Change == "Added");
        Assert.True(Assert.Single(comparison.Answers).Changed);
        Assert.Equal("Differs", comparison.Answers[0].ReferenceComparison);
        Assert.False(comparison.HealthRegression);
        Assert.Throws<ArgumentException>(() => ResolverScoreComparer.Compare(before, after, new[] { new ResolverAnswerReference { Name = "example.com", RecordType = DnsRecordType.A, Response = Answer("192.0.2.3") } }));
    }
}
