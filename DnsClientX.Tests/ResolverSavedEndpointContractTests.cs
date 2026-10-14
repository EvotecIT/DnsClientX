using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Protects persisted resolver settings independently of readable candidate labels.</summary>
public sealed class ResolverSavedEndpointContractTests {
    private static DnsResolverEndpoint ConfiguredEndpoint(bool doh) => new DnsResolverEndpoint {
        Host = "resolver.example", Port = doh ? 8443 : 8853, Transport = doh ? Transport.Doh : Transport.Dot,
        DohUrl = doh ? new Uri("https://resolver.example:8443/dns-query") : null,
        RequestFormat = doh ? DnsRequestFormat.DnsOverHttpsWirePost : DnsRequestFormatMapper.FromTransport(Transport.Dot),
        TlsServerName = "tls.resolver.example", Family = AddressFamily.InterNetworkV6,
        Timeout = TimeSpan.FromMilliseconds(1234), EdnsBufferSize = 1280, DnsSecOk = true,
        AllowTcpFallback = false
    };

    private static Task<ResolverProbeReport> ProbeAsync(DnsResolverEndpoint[] endpoints) => ResolverProbeWorkflow.RunAsync(
        ResolverExecutionPlanBuilder.BuildExplicitTargets(endpoints), "example.com", DnsRecordType.A, 2000,
        new ResolverQueryRunOptions(), new ResolverProbePolicy(),
        explicitOverride: (endpoint, name, type, token) => Task.FromResult(new ResolverQueryAttemptResult {
            Target = ResolverExecutionPlanBuilder.DescribeEndpoint(endpoint), Resolver = endpoint.Host!,
            RequestFormat = endpoint.RequestFormat ?? DnsRequestFormatMapper.FromTransport(endpoint.Transport),
            Response = new DnsResponse { Status = DnsResponseCode.NoError, Answers = new[] {
                new DnsAnswer { Name = name, Type = type, DataRaw = endpoint.Timeout!.Value.TotalMilliseconds == 1234 ? "192.0.2.1" : "192.0.2.2" }
            } }, Elapsed = endpoint.Timeout.Value
        }));

    /// <summary>Saving a recommendation preserves settings omitted from a readable endpoint label.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveLoadSelection_PreservesExplicitExecutionSettings(bool doh) {
        DnsResolverEndpoint endpoint = ConfiguredEndpoint(doh);
        string originalKey = ResolverEndpointClientFactory.GetExecutionKey(endpoint);
        ResolverProbeReport report = await ProbeAsync(new[] { endpoint });
        string path = Path.GetTempFileName();
        try {
            ResolverScoreStore.Save(path, report.Snapshot);
            ResolverScoreSnapshot loaded = ResolverScoreStore.Load(path);
            Assert.Equal(3, loaded.SchemaVersion);
            Assert.Equal(report.Results[0].CandidateId, loaded.Results[0].CandidateId);
            Assert.True(ResolverScoreSelector.TrySelectRecommended(loaded, out ResolverSelectionResult? selection, out string? error), error);
            Assert.Equal(loaded.Summary.RecommendedCandidateId, selection!.CandidateId);
            Assert.Equal(originalKey, ResolverEndpointClientFactory.GetExecutionKey(selection!.ExplicitEndpoint!));
            Assert.Equal(originalKey, ResolverEndpointClientFactory.GetExecutionKey(endpoint));
        } finally { File.Delete(path); }
    }

    /// <summary>Actual queries preserve distinct same-label candidates through scoring, comparison and selection.</summary>
    [Fact]
    public async Task ActualTcpWorkflows_SameLabelRemainDistinctAndReusable() {
        var server = new ResolverBenchmarkTcpFixture();
        try {
            var first = new DnsResolverEndpoint { Host = "127.0.0.1", Port = server.Port, Transport = Transport.Tcp, Timeout = TimeSpan.FromSeconds(1) };
            var second = new DnsResolverEndpoint { Host = "127.0.0.1", Port = server.Port, Transport = Transport.Tcp, Timeout = TimeSpan.FromSeconds(2) };
            ResolverExecutionTarget[] targets = ResolverExecutionPlanBuilder.BuildExplicitTargets(new[] { first, second });
            Assert.Equal(2, targets.Length);
            Assert.Equal(targets[0].DisplayName, targets[1].DisplayName);
            ResolverProbeReport probe = await ResolverProbeWorkflow.RunAsync(targets, "example.com", DnsRecordType.A, 2000, new ResolverQueryRunOptions(), new ResolverProbePolicy());
            ResolverBenchmarkReport benchmark = await ResolverBenchmarkWorkflow.RunAsync(targets, new[] { "example.com" }, new[] { DnsRecordType.A }, 1, 1, 2000, new ResolverQueryRunOptions(), new ResolverBenchmarkPolicy());
            Assert.Equal(4, server.Queries);
            Assert.Equal(2, probe.Results.Length);
            Assert.Equal(2, benchmark.Attempts.Length);
            foreach (ResolverScoreSnapshot snapshot in new[] { probe.Snapshot, benchmark.Snapshot }) {
                Assert.Equal(2, snapshot.Results.Length);
                Assert.Equal(2, snapshot.Results.Select(row => row.CandidateId).Distinct().Count());
                Assert.All(snapshot.Results, row => Assert.Equal(1, row.TotalQueries));
                Assert.True(ResolverScoreSelector.TrySelectRecommended(snapshot, out var selection, out string? error), error);
                var selectedTarget = Assert.Single(targets, target => target.CandidateId == selection!.CandidateId);
                Assert.Equal(ResolverEndpointClientFactory.GetExecutionKey(selectedTarget.ExplicitEndpoint!),
                    ResolverEndpointClientFactory.GetExecutionKey(selection!.ExplicitEndpoint!));
                ResolverScoreComparison comparison = ResolverScoreComparer.Compare(snapshot, snapshot);
                Assert.True(comparison.ScopeComparable, string.Join("; ", comparison.IncomparableReasons));
                Assert.Equal(2, comparison.Candidates.Length);
                Assert.Equal(2, comparison.Answers.Length);
                Assert.All(comparison.Candidates, row => Assert.Equal("Unchanged", row.Change));
                Assert.Single(snapshot.Results, row => row.IsRecommended);
            }
        } finally { await server.DisposeAsync(); }
    }

    /// <summary>Ranked probe rows retain the payload of their source attempt when readable labels collide.</summary>
    [Fact]
    public async Task Probe_SameLabelRanksPreserveSourceAttemptEvidence() {
        DnsResolverEndpoint first = ConfiguredEndpoint(false);
        var second = new DnsResolverEndpoint { Host = first.Host, Port = first.Port, Transport = first.Transport, Timeout = TimeSpan.FromMilliseconds(1500) };
        ResolverProbeReport report = await ProbeAsync(new[] { second, first });
        Assert.Equal(2, report.Results.Length);
        Assert.Equal(1234, report.Results[0].ElapsedMs);
        Assert.Contains("192.0.2.1", report.Results[0].AnswerSignature);
        Assert.Equal(1500, report.Results[1].ElapsedMs);
        Assert.Contains("192.0.2.2", report.Results[1].AnswerSignature);
        Assert.Single(report.Results, row => row.IsFastestSuccess);
        Assert.Equal(2, report.Summary.AnswerVariants.Length);
        var firstVariant = Assert.Single(report.Summary.AnswerVariants,
            variant => variant.CandidateIds.Contains(report.Results[0].CandidateId));
        Assert.Contains("192.0.2.1", firstVariant.AnswerSet);
        Assert.Single(report.Summary.MismatchedCandidateIds);
    }

    /// <summary>New snapshots cannot fall back to lossy labels when their saved metadata is missing or contradictory.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("inconsistent")]
    [InlineData("ambiguous")]
    [InlineData("identity-missing")]
    [InlineData("identity-and-context-missing")]
    public async Task Selection_RejectsIncompleteOrInconsistentNewEndpointMetadata(string defect) {
        ResolverProbeReport report = await ProbeAsync(new[] { ConfiguredEndpoint(true) });
        ResolverScoreRunContext context = report.Snapshot.Context!;
        string target = report.Snapshot.Summary.RecommendedCandidateId;
        switch (defect) {
            case "missing": context.ExplicitEndpoints = null; break;
            case "inconsistent": context.ExplicitEndpoints![target] = ConfiguredEndpoint(false); break;
            case "ambiguous":
                report.Snapshot.Results = report.Snapshot.Results.Concat(report.Snapshot.Results).ToArray();
                break;
            case "identity-missing": context.TargetExecutionKeys = null; break;
            case "identity-and-context-missing":
                report.Snapshot.Summary.RecommendedCandidateId = string.Empty;
                report.Snapshot.Context = null;
                foreach (var row in report.Snapshot.Results) row.CandidateId = string.Empty;
                break;
        }
        Assert.False(ResolverScoreSelector.TrySelectRecommended(report.Snapshot, out ResolverSelectionResult? selection, out string? error));
        Assert.Null(selection);
        Assert.Contains("saved", error!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Current-schema built-in recommendations reuse their captured candidate and execution identity.</summary>
    [Fact]
    public async Task Selection_CurrentBuiltInRetainsExecutionIdentity() {
        ResolverExecutionTarget[] targets = ResolverExecutionPlanBuilder.BuildBuiltInTargets(new[] { DnsEndpoint.Cloudflare });
        ResolverProbeReport report = await ResolverProbeWorkflow.RunAsync(targets, "example.com", DnsRecordType.A, 2000,
            new ResolverQueryRunOptions(), new ResolverProbePolicy(),
            builtInOverride: (endpoint, name, type, token) => Task.FromResult(new ResolverQueryAttemptResult {
                Target = endpoint.ToString(), Response = new DnsResponse { Status = DnsResponseCode.NoError },
                Elapsed = TimeSpan.FromMilliseconds(1)
            }));
        Assert.Equal(3, report.Snapshot.SchemaVersion);
        Assert.True(ResolverScoreSelector.TrySelectRecommended(report.Snapshot, out var selection, out string? error), error);
        Assert.Equal(DnsEndpoint.Cloudflare, selection!.BuiltInEndpoint);
        Assert.Equal(targets[0].CandidateId, selection.CandidateId);
    }

    /// <summary>Genuinely older snapshots retain the established label-parser selection behavior.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Selection_LegacySnapshotRetainsLabelFallback(int schemaVersion) {
        var snapshot = new ResolverScoreSnapshot { SchemaVersion = schemaVersion, Summary = new ResolverScoreSummary {
            RecommendationAvailable = true, RecommendedTarget = "tls@resolver.example:8853"
        } };
        Assert.True(ResolverScoreSelector.TrySelectRecommended(snapshot, out ResolverSelectionResult? selection, out string? error), error);
        Assert.Equal("resolver.example", selection!.ExplicitEndpoint!.Host);
        Assert.Equal(8853, selection.ExplicitEndpoint.Port);
        Assert.Equal(Transport.Dot, selection.ExplicitEndpoint.Transport);
    }
}
