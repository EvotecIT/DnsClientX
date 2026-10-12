using System;
using System.Linq;

namespace DnsClientX {
    /// <summary>Describes a portable, repeatable resolver health query catalog and its policy.</summary>
    public sealed class ResolverHealthProfile {
        /// <summary>Gets the supported profile schema version.</summary>
        public const int CurrentSchemaVersion = 1;
        /// <summary>Gets or sets the profile schema version.</summary>
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        /// <summary>Gets or sets a descriptive profile name.</summary>
        public string Name { get; set; } = "resolver-health";
        /// <summary>Gets or sets built-in targets. Use these or explicit endpoints.</summary>
        public DnsEndpoint[] BuiltInEndpoints { get; set; } = Array.Empty<DnsEndpoint>();
        /// <summary>Gets or sets portable inline resolver endpoints with distinct execution settings, such as udp@1.1.1.1:53.</summary>
        public string[] ResolverEndpoints { get; set; } = Array.Empty<string>();
        /// <summary>Gets or sets the DNS names in the query catalog.</summary>
        public string[] Names { get; set; } = Array.Empty<string>();
        /// <summary>Gets or sets the record types queried for every name.</summary>
        public DnsRecordType[] RecordTypes { get; set; } = new[] { DnsRecordType.A };
        /// <summary>Gets or sets the timeout per query in milliseconds.</summary>
        public int TimeoutMs { get; set; } = 2000;
        /// <summary>Gets or sets the number of benchmark attempts per combination.</summary>
        public int AttemptsPerCombination { get; set; } = 3;
        /// <summary>Gets or sets the maximum concurrent benchmark requests.</summary>
        public int MaxConcurrency { get; set; } = 4;
        /// <summary>Gets or sets benchmark connection reuse.</summary>
        public ResolverQueryConnectionMode ConnectionMode { get; set; } = ResolverQueryConnectionMode.Cold;
        /// <summary>Gets or sets whether DNSSEC material is requested.</summary>
        public bool RequestDnsSec { get; set; }
        /// <summary>Gets or sets whether DNSSEC is validated locally.</summary>
        public bool ValidateDnsSec { get; set; }
        /// <summary>Gets or sets whether EDNS NSID is requested.</summary>
        public bool RequestNsid { get; set; }
        /// <summary>Gets or sets an IP-literal UDP/TCP bootstrap endpoint.</summary>
        public string? BootstrapEndpoint { get; set; }
        /// <summary>Gets or sets the gates applied independently to every probe query.</summary>
        public ResolverProbePolicy ProbePolicy { get; set; } = new ResolverProbePolicy();
        /// <summary>Gets or sets the gates applied to an optional benchmark run.</summary>
        public ResolverBenchmarkPolicy BenchmarkPolicy { get; set; } = new ResolverBenchmarkPolicy();

        /// <summary>Validates the schema, targets, query catalog, workload bounds, and thresholds.</summary>
        public void Validate() {
            if (SchemaVersion != CurrentSchemaVersion) {
                throw new InvalidOperationException($"Unsupported resolver health profile schema version {SchemaVersion}; expected {CurrentSchemaVersion}.");
            }
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 256) {
                throw new ArgumentException("A profile name of 1 through 256 characters is required.");
            }
            if (BuiltInEndpoints == null || ResolverEndpoints == null || Names == null || RecordTypes == null || ProbePolicy == null || BenchmarkPolicy == null) {
                throw new ArgumentException("Profile arrays and policies cannot be null.");
            }
            if ((BuiltInEndpoints.Length == 0) == (ResolverEndpoints.Length == 0)) {
                throw new ArgumentException("Specify either BuiltInEndpoints or ResolverEndpoints in the profile.");
            }
            int count = BuiltInEndpoints.Length + ResolverEndpoints.Length;
            if (count > 64 || BuiltInEndpoints.Any(e => !Enum.IsDefined(typeof(DnsEndpoint), e) || e == DnsEndpoint.Custom) || BuiltInEndpoints.Distinct().Count() != BuiltInEndpoints.Length) {
                throw new ArgumentException("Use at most 64 distinct supported resolver targets.");
            }
            if (ResolverEndpoints.Any(e => string.IsNullOrWhiteSpace(e) || e.Length > 2048)) {
                throw new ArgumentException("Resolver endpoint values must contain 1 through 2048 characters.");
            }
            DnsResolverEndpoint[] parsedEndpoints = EndpointParser.TryParseMany(ResolverEndpoints, out var endpointErrors);
            if (endpointErrors.Count > 0) {
                throw new ArgumentException(string.Join("; ", endpointErrors));
            }
            if (ResolverEndpoints.Length > 0) {
                count = parsedEndpoints.Select(ResolverEndpointClientFactory.GetExecutionKey).Distinct(StringComparer.Ordinal).Count();
                if (count != parsedEndpoints.Length) {
                    throw new ArgumentException("Profile resolver targets must identify distinct executions.");
                }
                if (count > 64) {
                    throw new ArgumentException("Use at most 64 distinct supported resolver targets.");
                }
            }
            if (Names.Length < 1 || Names.Length > 64 || Names.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 253)) {
                throw new ArgumentException("Use 1 through 64 DNS names, each at most 253 characters.");
            }
            if (Names.Select(n => n.TrimEnd('.')).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Names.Length || RecordTypes.Distinct().Count() != RecordTypes.Length) {
                throw new ArgumentException("The query catalog cannot contain duplicate names or types.");
            }
            if (RecordTypes.Length < 1 || RecordTypes.Length > 16 || RecordTypes.Any(t => !Enum.IsDefined(typeof(DnsRecordType), t) || t == DnsRecordType.AXFR || t == DnsRecordType.IXFR)) {
                throw new ArgumentException("Use 1 through 16 supported query types; zone transfers are separate workflows.");
            }
            if (TimeoutMs < 1 || TimeoutMs > 300000 || MaxConcurrency < 1 || MaxConcurrency > 256 || AttemptsPerCombination < 1 || AttemptsPerCombination > 1000) {
                throw new ArgumentException("Timeout, concurrency, or attempt count is outside the profile limits.");
            }
            if ((long)count * Names.Length * RecordTypes.Length * AttemptsPerCombination > 65536) {
                throw new ArgumentException("The profile exceeds 65536 benchmark attempts.");
            }
            if (ConnectionMode != ResolverQueryConnectionMode.Cold && ConnectionMode != ResolverQueryConnectionMode.Warm) {
                throw new ArgumentException("Profile connections must be Cold or Warm.");
            }
            if (!string.IsNullOrWhiteSpace(BootstrapEndpoint)) {
                EndpointParser.ParseBootstrap(BootstrapEndpoint!);
            }
            ValidatePercent(ProbePolicy.MinSuccessPercent);
            ValidatePercent(ProbePolicy.MinConsensusPercent);
            ValidatePercent(BenchmarkPolicy.MinSuccessPercent);
            if (ProbePolicy.MinSuccessCount < 1 || ProbePolicy.MinSuccessCount > count || BenchmarkPolicy.MinSuccessfulCandidates < 1 || BenchmarkPolicy.MinSuccessfulCandidates > count) {
                throw new ArgumentException("A minimum candidate count must be between 1 and the target count.");
            }
        }

        private static void ValidatePercent(int? value) {
            if (value < 1 || value > 100) {
                throw new ArgumentException("Profile percentages must be between 1 and 100.");
            }
        }
    }
}
