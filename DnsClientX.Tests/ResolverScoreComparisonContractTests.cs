using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Protects comparison scope against hidden endpoint differences and raw authoritative response envelopes.</summary>
public sealed class ResolverScoreComparisonContractTests {
    private static DnsResolverEndpoint Endpoint(string setting, bool alternate) => new DnsResolverEndpoint {
        Host = setting == "tls" ? "192.0.2.1" : "resolver.example",
        Transport = setting == "doh-post" ? Transport.Doh : setting == "tls" ? Transport.Dot : setting == "edns" || setting == "dnssec" || setting == "fallback" ? Transport.Udp : Transport.Tcp,
        DohUrl = setting == "doh-post" ? new Uri("https://resolver.example/dns-query") : null,
        RequestFormat = setting == "doh-post" ? alternate ? DnsRequestFormat.DnsOverHttpsWirePost : DnsRequestFormat.DnsOverHttps : null,
        Timeout = setting == "timeout" ? TimeSpan.FromSeconds(alternate ? 2 : 1) : null,
        Family = setting == "family" ? alternate ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork : null,
        TlsServerName = setting == "tls" ? alternate ? "second.example" : "first.example" : null,
        EdnsBufferSize = setting == "edns" ? alternate ? 1280 : 1232 : null,
        DnsSecOk = setting == "dnssec" ? alternate : null,
        AllowTcpFallback = setting != "fallback" || alternate
    };

    private static DnsResponse Answer(string owner = "example.com") => new DnsResponse {
        Status = DnsResponseCode.NoError,
        Answers = new[] { new DnsAnswer { Name = owner, Type = DnsRecordType.A, DataRaw = "192.0.2.1", TTL = 60 } }
    };

    private static Task<ResolverProbeReport> ProbeAsync(DnsResolverEndpoint endpoint, ResolverQueryRunOptions? options = null) => ResolverProbeWorkflow.RunAsync(
        ResolverExecutionPlanBuilder.BuildExplicitTargets(new[] { endpoint }), "example.com", DnsRecordType.A, 2000,
        options ?? new ResolverQueryRunOptions(), new ResolverProbePolicy(),
        explicitOverride: (configured, name, type, token) => Task.FromResult(new ResolverQueryAttemptResult {
            Target = ResolverExecutionPlanBuilder.DescribeEndpoint(configured), Resolver = "resolver.example",
            RequestFormat = configured.RequestFormat ?? DnsRequestFormatMapper.FromTransport(configured.Transport),
            Response = Answer(), Elapsed = TimeSpan.FromMilliseconds(5)
        }));

    /// <summary>Readable labels cannot erase changes that affect execution or DNS security.</summary>
    [Theory]
    [InlineData("timeout")]
    [InlineData("family")]
    [InlineData("tls")]
    [InlineData("edns")]
    [InlineData("dnssec")]
    [InlineData("fallback")]
    [InlineData("doh-post")]
    public async Task Compare_RejectsSameLabelWithDifferentExecutionSettings(string setting) {
        DnsResolverEndpoint first = Endpoint(setting, false), second = Endpoint(setting, true);
        Assert.Equal(ResolverExecutionPlanBuilder.DescribeEndpoint(first), ResolverExecutionPlanBuilder.DescribeEndpoint(second));
        ResolverProbeReport before = await ProbeAsync(first), after = await ProbeAsync(second);
        ResolverScoreComparison comparison = ResolverScoreComparer.Compare(before.Snapshot, after.Snapshot);
        Assert.False(comparison.ScopeComparable);
        Assert.Equal(2, comparison.Candidates.Length);
        Assert.Contains(comparison.Candidates, candidate => candidate.Change == "Added");
        Assert.Contains(comparison.Candidates, candidate => candidate.Change == "Removed");
        Assert.All(comparison.Candidates, candidate => Assert.Null(candidate.AverageMsDelta));
    }

    /// <summary>Real probe and benchmark snapshot capture retain endpoint timeouts beyond display labels.</summary>
    [Fact]
    public async Task SnapshotCapture_ActualTcpWorkflowRetainsEndpointExecutionScope() {
        var server = new ResolverBenchmarkTcpFixture();
        try {
            var first = new DnsResolverEndpoint { Host = "127.0.0.1", Port = server.Port, Transport = Transport.Tcp, Timeout = TimeSpan.FromSeconds(1) };
            var second = new DnsResolverEndpoint { Host = "127.0.0.1", Port = server.Port, Transport = Transport.Tcp, Timeout = TimeSpan.FromSeconds(2) };
            ResolverProbeReport before = await ResolverProbeWorkflow.RunAsync(ResolverExecutionPlanBuilder.BuildExplicitTargets(new[] { first }), "example.com", DnsRecordType.A, 2000, new ResolverQueryRunOptions(), new ResolverProbePolicy());
            ResolverBenchmarkReport after = await ResolverBenchmarkWorkflow.RunAsync(ResolverExecutionPlanBuilder.BuildExplicitTargets(new[] { second }), new[] { "example.com" }, new[] { DnsRecordType.A }, 1, 1, 2000, new ResolverQueryRunOptions(), new ResolverBenchmarkPolicy());
            // Different workflows are already incomparable; isolate the persisted endpoint identity below.
            after.Snapshot.Summary.Mode = before.Snapshot.Summary.Mode;
            after.Snapshot.Summary.MaxConcurrency = before.Snapshot.Summary.MaxConcurrency;
            Assert.Equal(2, server.Queries);
            Assert.Equal(ResolverEndpointClientFactory.GetExecutionKey(first), before.Snapshot.Context!.TargetExecutionKeys![before.Results[0].CandidateId]);
            Assert.Equal(ResolverEndpointClientFactory.GetExecutionKey(second), after.Snapshot.Context!.TargetExecutionKeys![after.Results[0].CandidateId]);
            Assert.False(ResolverScoreComparer.Compare(before.Snapshot, after.Snapshot).ScopeComparable);
        } finally { await server.DisposeAsync(); }
    }

    /// <summary>Raw authoritative aliases and signatures compare using the normal requested answer projection.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reference_UsesRequestedProjectionWithoutMutatingCaller(bool alias) {
        ResolverProbeReport report = await ProbeAsync(Endpoint("timeout", false));
        string owner = alias ? "target.example" : "example.com";
        DnsResponse reference = Answer(owner);
        reference.IsAuthoritativeAnswer = true;
        reference.Questions = new[] { new DnsQuestion { Name = "example.com", Type = DnsRecordType.A } };
        reference.Answers = reference.Answers.Concat(new[] { new DnsAnswer {
            Name = "example.com", Type = alias ? DnsRecordType.CNAME : DnsRecordType.RRSIG,
            DataRaw = alias ? "target.example" : "A 8 2 60 20270101000000 20261001000000 12345 example.com signature"
        } }).ToArray();
        report.Snapshot.Answers[0].AnswerSignature = DnsResponseAnswerSignature.Build(Answer(owner));
        ResolverScoreComparison comparison = ResolverScoreComparer.Compare(report.Snapshot, report.Snapshot, new[] {
            new ResolverAnswerReference { Name = "example.com", RecordType = DnsRecordType.A, Response = reference }
        });
        Assert.Equal("Matches", Assert.Single(comparison.Answers).ReferenceComparison);
        Assert.Equal(2, reference.Answers.Length);
        Assert.False(reference.RequestedAnswerPresent);
    }

    /// <summary>Legacy context cannot establish an execution identity, even when its readable labels match.</summary>
    [Fact]
    public async Task Compare_MissingExecutionIdentityWithholdsRegressionConclusions() {
        ResolverProbeReport before = await ProbeAsync(Endpoint("timeout", false)), after = await ProbeAsync(Endpoint("timeout", false));
        before.Snapshot.Summary.PolicyPassed = true;
        after.Snapshot.Summary.PolicyPassed = false;
        after.Snapshot.Context!.TargetExecutionKeys = null;
        ResolverScoreComparison comparison = ResolverScoreComparer.Compare(before.Snapshot, after.Snapshot);
        Assert.False(comparison.ScopeComparable);
        Assert.False(comparison.HealthRegression);
        Assert.Null(Assert.Single(comparison.Candidates).AverageMsDelta);
    }

    /// <summary>The bootstrap's endpoint settings use the same canonical identity as query targets.</summary>
    [Fact]
    public async Task Compare_BootstrapIdentityPreservesHiddenSettings() {
        var first = new DnsResolverEndpoint { Host = "127.0.0.1", Transport = Transport.Udp, Timeout = TimeSpan.FromSeconds(1) };
        var second = new DnsResolverEndpoint { Host = "127.0.0.1", Transport = Transport.Udp, Timeout = TimeSpan.FromSeconds(2) };
        Assert.Equal(ResolverExecutionPlanBuilder.DescribeEndpoint(first), ResolverExecutionPlanBuilder.DescribeEndpoint(second));
        ResolverProbeReport before = await ProbeAsync(Endpoint("timeout", false), new ResolverQueryRunOptions { BootstrapResolver = first });
        ResolverProbeReport after = await ProbeAsync(Endpoint("timeout", false), new ResolverQueryRunOptions { BootstrapResolver = second });
        Assert.Equal(ResolverEndpointClientFactory.GetExecutionKey(first), before.Snapshot.Context!.BootstrapEndpoint);
        Assert.False(ResolverScoreComparer.Compare(before.Snapshot, after.Snapshot).ScopeComparable);
    }

    /// <summary>Different configured private trust boundaries cannot produce a comparable DNSSEC health verdict.</summary>
    [Fact]
    public async Task Compare_PrivateTrustAnchorChangeWithholdsHealthRegression() {
        ResolverQueryRunOptions Options(char digest) => new() {
            RequestDnsSec = true, ValidateDnsSec = true,
            DnsSecTrustAnchors = new[] { new DnsSecZoneTrustAnchor("example.com",
                new DnsSecTrustAnchor(1234, DnsKeyAlgorithm.RSASHA256, 2, new string(digest, 64))) }
        };
        ResolverProbeReport before = await ProbeAsync(Endpoint("timeout", false), Options('A'));
        ResolverProbeReport after = await ProbeAsync(Endpoint("timeout", false), Options('B'));
        before.Snapshot.Summary.PolicyPassed = true;
        after.Snapshot.Summary.PolicyPassed = false;
        ResolverScoreComparison comparison = ResolverScoreComparer.Compare(before.Snapshot, after.Snapshot);
        Assert.False(comparison.ScopeComparable);
        Assert.False(comparison.HealthRegression);
        Assert.Null(Assert.Single(comparison.Candidates).AverageMsDelta);
    }
}
