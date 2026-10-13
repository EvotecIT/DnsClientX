using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>Applies a validated health profile to the existing probe and benchmark engines.</summary>
    public static class ResolverHealthWorkflow {
        /// <summary>Runs all profile queries and optionally benchmarks the same catalog. Benchmark candidates must have distinct display labels. Programmatic verifier providers remain supplied by the caller.</summary>
        public static async Task<ResolverHealthReport> RunAsync(
            ResolverHealthProfile profile,
            bool includeBenchmark = false,
            IDnsSecSignatureVerifier? dnsSecSignatureVerifier = null,
            Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? builtInOverride = null,
            Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? explicitOverride = null,
            CancellationToken cancellationToken = default) {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            var source = new ResolverExecutionTargetSource { BuiltInEndpoints = profile.BuiltInEndpoints, ResolverEndpoints = profile.ResolverEndpoints };
            ResolverExecutionTarget[] targets = await ResolverExecutionTargetResolver.ResolveAsync(source, cancellationToken).ConfigureAwait(false);
            if (includeBenchmark && targets.GroupBy(target => target.DisplayName, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) {
                throw new ArgumentException("Benchmark endpoints share a candidate display label. Use separate benchmark profiles until reports can distinguish their execution settings.", nameof(profile));
            }
            var options = new ResolverQueryRunOptions {
                TimeoutMs = profile.TimeoutMs, ConnectionMode = profile.ConnectionMode,
                RequestDnsSec = profile.RequestDnsSec, ValidateDnsSec = profile.ValidateDnsSec,
                RequestNsid = profile.RequestNsid, DnsSecSignatureVerifier = dnsSecSignatureVerifier,
                BootstrapResolver = string.IsNullOrWhiteSpace(profile.BootstrapEndpoint) ? null : EndpointParser.ParseBootstrap(profile.BootstrapEndpoint!)
            };
            var queries = new List<ResolverProbeReport>();
            foreach (string name in profile.Names) {
                foreach (DnsRecordType type in profile.RecordTypes) {
                    cancellationToken.ThrowIfCancellationRequested();
                    queries.Add(await ResolverProbeWorkflow.RunAsync(targets, name, type, profile.TimeoutMs, options, profile.ProbePolicy,
                        builtInOverride: builtInOverride, explicitOverride: explicitOverride, cancellationToken: cancellationToken).ConfigureAwait(false));
                }
            }
            ResolverBenchmarkReport? benchmark = includeBenchmark
                ? await ResolverBenchmarkWorkflow.RunAsync(targets, profile.Names, profile.RecordTypes, profile.AttemptsPerCombination, profile.MaxConcurrency,
                    profile.TimeoutMs, options, profile.BenchmarkPolicy, builtInOverride: builtInOverride, explicitOverride: explicitOverride, cancellationToken: cancellationToken).ConfigureAwait(false)
                : null;
            return new ResolverHealthReport { ProfileName = profile.Name, Queries = queries.ToArray(), Benchmark = benchmark,
                PolicyPassed = queries.All(q => q.Evaluation.PolicyPassed && q.Evaluation.SuccessfulCandidates > 0) && (benchmark == null || benchmark.Evaluation.PolicyPassed && benchmark.Evaluation.SuccessfulCandidates > 0) };
        }
    }
}
