using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Protects portable profile application and correlated execution through the existing engines.</summary>
public sealed class ResolverHealthWorkflowTests {
    /// <summary>Equivalent endpoint inputs cannot inflate candidate gates or dispatch through profile consumers.</summary>
    [Theory]
    [InlineData("udp@127.0.0.1:53", "udp@127.0.0.1:53")]
    [InlineData("127.0.0.1", "udp@127.0.0.1:53")]
    [InlineData("dot@resolver.example", "tls@RESOLVER.EXAMPLE:853")]
    [InlineData("https://resolver.example/dns-query", "doh@https://resolver.example:443/dns-query")]
    public async Task Profile_RejectsEquivalentExecutionTargetsBeforeStorageOrDispatch(string first, string second) {
        var profile = new ResolverHealthProfile {
            ResolverEndpoints = new[] { first, second }, Names = new[] { "example.com" },
            ProbePolicy = new ResolverProbePolicy { MinSuccessCount = 2 },
            BenchmarkPolicy = new ResolverBenchmarkPolicy { MinSuccessfulCandidates = 2 }
        };
        DnsResolverEndpoint[] parsed = EndpointParser.TryParseMany(profile.ResolverEndpoints, out var errors);
        Assert.Empty(errors);
        Assert.Equal(2, parsed.Length);
        Assert.Single(ResolverExecutionPlanBuilder.BuildExplicitTargets(parsed));
        ArgumentException error = Assert.Throws<ArgumentException>(() => profile.Validate());
        Assert.Contains("distinct", error.Message, StringComparison.OrdinalIgnoreCase);
        string path = Path.GetTempFileName();
        try {
            Assert.Throws<ArgumentException>(() => ResolverHealthProfileStore.Save(path, profile));
            File.WriteAllText(path, DnsClientXJsonSerializer.Serialize(profile));
            Assert.Throws<ArgumentException>(() => ResolverHealthProfileStore.Load(path));
        } finally { File.Delete(path); }
        int dispatched = 0;
        await Assert.ThrowsAsync<ArgumentException>(() => ResolverHealthWorkflow.RunAsync(profile, includeBenchmark: true,
            explicitOverride: (endpoint, name, type, token) => {
                Interlocked.Increment(ref dispatched);
                return Task.FromResult(new ResolverQueryAttemptResult { Response = new DnsResponse { Status = DnsResponseCode.NoError } });
            }));
        Assert.Equal(0, dispatched);
    }

    /// <summary>Distinct DNSSEC settings remain separate executions even when endpoint display labels match.</summary>
    [Fact]
    public async Task Profile_AllowsSameLabelWithDistinctExecutionSettings() {
        string[] inputs = new[] { false, true }.Select(dnsSec => DnsStamp.Create(new DnsResolverEndpoint {
            Host = "127.0.0.1", Port = 53, Transport = Transport.Udp, DnsSecOk = dnsSec
        })).ToArray();
        var profile = new ResolverHealthProfile {
            ResolverEndpoints = inputs, Names = new[] { "example.com" },
            ProbePolicy = new ResolverProbePolicy { MinSuccessCount = 2 },
            BenchmarkPolicy = new ResolverBenchmarkPolicy { MinSuccessfulCandidates = 2 }
        };
        profile.Validate();
        DnsResolverEndpoint[] parsed = EndpointParser.TryParseMany(inputs, out var errors);
        Assert.Empty(errors);
        ResolverExecutionTarget[] targets = ResolverExecutionPlanBuilder.BuildExplicitTargets(parsed);
        Assert.Equal(2, targets.Length);
        Assert.Equal(targets[0].DisplayName, targets[1].DisplayName);
        int dispatched = 0;
        ResolverHealthReport report = await ResolverHealthWorkflow.RunAsync(profile,
            explicitOverride: (endpoint, name, type, token) => {
                Interlocked.Increment(ref dispatched);
                return Task.FromResult(new ResolverQueryAttemptResult {
                    Target = ResolverExecutionPlanBuilder.DescribeEndpoint(endpoint),
                    Response = new DnsResponse { Status = DnsResponseCode.NoError }
                });
            });
        Assert.True(report.PolicyPassed);
        Assert.Equal(2, Assert.Single(report.Queries).Results.Length);
        Assert.Equal(2, dispatched);
        Assert.Equal(inputs, profile.ResolverEndpoints);
    }

    /// <summary>Benchmark profiles score distinct executions even when their readable labels match.</summary>
    [Fact]
    public async Task Profile_BenchmarkScoresSameLabelSettingsSeparately() {
        var profile = new ResolverHealthProfile {
            ResolverEndpoints = new[] { false, true }.Select(dnsSec => DnsStamp.Create(new DnsResolverEndpoint {
                Host = "127.0.0.1", Port = 53, Transport = Transport.Udp, DnsSecOk = dnsSec
            })).ToArray(),
            Names = new[] { "example.com" }, RecordTypes = new[] { DnsRecordType.A },
            AttemptsPerCombination = 1, ProbePolicy = new ResolverProbePolicy { MinSuccessCount = 2 }
        };
        int dispatched = 0;
        ResolverHealthReport report = await ResolverHealthWorkflow.RunAsync(profile,
            includeBenchmark: true, explicitOverride: (endpoint, name, type, token) => {
                Interlocked.Increment(ref dispatched);
                return Task.FromResult(new ResolverQueryAttemptResult {
                    Target = ResolverExecutionPlanBuilder.DescribeEndpoint(endpoint),
                    Response = new DnsResponse { Status = DnsResponseCode.NoError }
                });
            });
        Assert.Equal(4, dispatched);
        Assert.Equal(2, report.Benchmark!.Results.Length);
        Assert.Equal(2, report.Benchmark.Results.Select(row => row.CandidateId).Distinct().Count());
        Assert.All(report.Benchmark.Results, row => Assert.Equal(1, row.TotalQueries));
    }

    /// <summary>Exercises an actual TCP target with a multi-query profile and bounded benchmark.</summary>
    [Fact]
    public async Task Profile_UsesActualEndpointAndCorrelatesBenchmarkCatalog() {
        var server = new ResolverBenchmarkTcpFixture();
        try {
            var profile = new ResolverHealthProfile {
                ResolverEndpoints = new[] { $"tcp@127.0.0.1:{server.Port}" }, Names = new[] { "one.example", "two.example" },
                RecordTypes = new[] { DnsRecordType.A, DnsRecordType.AAAA }, AttemptsPerCombination = 2,
                MaxConcurrency = 2, TimeoutMs = 1500, ProbePolicy = new ResolverProbePolicy { MinSuccessCount = 1 },
                BenchmarkPolicy = new ResolverBenchmarkPolicy { MinSuccessPercent = 100 }
            };
            ResolverHealthReport report = await ResolverHealthWorkflow.RunAsync(profile, includeBenchmark: true);
            Assert.True(report.PolicyPassed);
            Assert.Equal(4, report.Queries.Length);
            Assert.All(report.Queries, q => Assert.Equal(1500, q.Summary.TimeoutMs));
            Assert.Equal(12, server.Queries);
            Assert.Equal(8, report.Benchmark!.Snapshot.Answers.Length);
            Assert.Equal(4, report.Benchmark.Snapshot.Answers.Select(a => (a.Name, a.RecordType)).Distinct().Count());
            Assert.NotNull(report.Benchmark.Snapshot.Context);
            Assert.All(report.Queries, q => Assert.Single(q.Snapshot.Answers));
        } finally { await server.DisposeAsync(); }
    }

    /// <summary>Protects bounded JSON loading and persisted thresholds rather than prose/configuration shape.</summary>
    [Fact]
    public void ProfileStore_RoundTripsAndRejectsUnknownSchemaAndOversizedInput() {
        string path = Path.GetTempFileName();
        try {
            var profile = new ResolverHealthProfile { BuiltInEndpoints = new[] { DnsEndpoint.System }, Names = new[] { "example.com" }, ProbePolicy = new ResolverProbePolicy { MinSuccessPercent = 100 } };
            ResolverHealthProfileStore.Save(path, profile);
            Assert.Equal(100, ResolverHealthProfileStore.Load(path).ProbePolicy.MinSuccessPercent);
            profile.SchemaVersion = 2;
            File.WriteAllText(path, DnsClientXJsonSerializer.Serialize(profile));
            Assert.Throws<InvalidOperationException>(() => ResolverHealthProfileStore.Load(path));
            File.WriteAllText(path, new string(' ', ResolverHealthProfileStore.MaximumProfileBytes + 1));
            Assert.Throws<InvalidOperationException>(() => ResolverHealthProfileStore.Load(path));
        } finally { File.Delete(path); }
    }

    /// <summary>Prevents an unbounded request catalog from dispatching and preserves caller cancellation.</summary>
    [Fact]
    public async Task Profile_RejectsOversizedWorkloadAndHonorsCanceledCaller() {
        var profile = new ResolverHealthProfile { BuiltInEndpoints = new[] { DnsEndpoint.System }, Names = Enumerable.Range(0, 64).Select(n => $"name{n}.example").ToArray(), RecordTypes = new[] { DnsRecordType.A, DnsRecordType.AAAA }, AttemptsPerCombination = 1000 };
        Assert.Throws<ArgumentException>(() => profile.Validate());
        profile.Names = new[] { "example.com" }; profile.AttemptsPerCombination = 1;
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ResolverHealthWorkflow.RunAsync(profile, cancellationToken: cancellation.Token));
    }

    /// <summary>The profile's per-query threshold changes the outcome when one catalog entry fails.</summary>
    [Fact]
    public async Task Profile_AppliesPerQueryPolicyRatherThanOverallMajority() {
        var profile = new ResolverHealthProfile { BuiltInEndpoints = new[] { DnsEndpoint.System }, Names = new[] { "good.example", "failed.example" }, ProbePolicy = new ResolverProbePolicy { MinSuccessCount = 1 } };
        ResolverHealthReport report = await ResolverHealthWorkflow.RunAsync(profile, builtInOverride: (endpoint, name, type, token) => Task.FromResult(new ResolverQueryAttemptResult {
            Target = "System", Response = new DnsResponse { Status = name == "good.example" ? DnsResponseCode.NoError : DnsResponseCode.ServerFailure }
        }));
        Assert.False(report.PolicyPassed);
        Assert.True(report.Queries[0].Evaluation.PolicyPassed);
        Assert.False(report.Queries[1].Evaluation.PolicyPassed);
    }
}
