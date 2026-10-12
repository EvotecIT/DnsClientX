using System;

namespace DnsClientX {
    /// <summary>Describes factual snapshot differences and whether their health/performance scopes are comparable.</summary>
    public sealed class ResolverScoreComparison {
        /// <summary>Gets or sets whether host, runtime, query, target, and policy scopes match.</summary>
        public bool ScopeComparable { get; set; }
        /// <summary>Gets or sets why health/performance regression conclusions are unavailable.</summary>
        public string[] IncomparableReasons { get; set; } = Array.Empty<string>();
        /// <summary>Gets or sets the baseline run policy result.</summary>
        public bool BaselinePolicyPassed { get; set; }
        /// <summary>Gets or sets the current run policy result.</summary>
        public bool CurrentPolicyPassed { get; set; }
        /// <summary>Gets or sets whether a comparable run changed from passing to failing.</summary>
        public bool HealthRegression { get; set; }
        /// <summary>Gets or sets candidate additions, removals, and score changes.</summary>
        public ResolverScoreChange[] Candidates { get; set; } = Array.Empty<ResolverScoreChange>();
        /// <summary>Gets or sets per-query answer/status changes and reference comparisons.</summary>
        public ResolverAnswerChange[] Answers { get; set; } = Array.Empty<ResolverAnswerChange>();
        /// <summary>Gets or sets the interpretation boundary for answer differences.</summary>
        public string AnswerVariationNote { get; set; } = "Answer changes are observations, not correctness failures. Location, cache age, and split DNS policy can produce legitimate differences. Caller-supplied authoritative references are not independently authenticated; resolver majority is not authoritative evidence.";
    }
    /// <summary>Describes one resolver's addition, removal, or changed score.</summary>
    public sealed class ResolverScoreChange {
        /// <summary>Gets or sets the target.</summary>
        public string Target { get; set; } = string.Empty;
        /// <summary>Gets or sets Added, Removed, Changed, Unchanged, or Ambiguous when the label cannot identify one execution.</summary>
        public string Change { get; set; } = string.Empty;
        /// <summary>Gets or sets the baseline score, unavailable for ambiguous labels.</summary>
        public ResolverScoreEntry? Baseline { get; set; }
        /// <summary>Gets or sets the current score, unavailable for ambiguous labels.</summary>
        public ResolverScoreEntry? Current { get; set; }
        /// <summary>Gets or sets the success percentage-point delta only for retained targets with comparable execution scopes.</summary>
        public int? SuccessPercentDelta { get; set; }
        /// <summary>Gets or sets the latency delta only when scopes match and both runs contain successes.</summary>
        public double? AverageMsDelta { get; set; }
    }
    /// <summary>Describes one resolver/query's observed answer variants and optional authoritative reference.</summary>
    public sealed class ResolverAnswerChange {
        /// <summary>Gets or sets the target.</summary>
        public string Target { get; set; } = string.Empty;
        /// <summary>Gets or sets the query name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the query type.</summary>
        public DnsRecordType RecordType { get; set; }
        /// <summary>Gets or sets whether observed variants changed, or null when one side lacks query evidence.</summary>
        public bool? Changed { get; set; }
        /// <summary>Gets or sets the baseline observations, absent for older snapshots without evidence.</summary>
        public ResolverAnswerObservation[] Baseline { get; set; } = Array.Empty<ResolverAnswerObservation>();
        /// <summary>Gets or sets the current observations.</summary>
        public ResolverAnswerObservation[] Current { get; set; } = Array.Empty<ResolverAnswerObservation>();
        /// <summary>Gets or sets Unavailable, Matches, Differs, or Mixed relative to the caller-supplied reference.</summary>
        public string ReferenceComparison { get; set; } = "Unavailable";
        /// <summary>Gets or sets the caller's reference source description.</summary>
        public string? ReferenceSource { get; set; }
    }
}
