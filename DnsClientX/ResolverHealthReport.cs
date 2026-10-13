using System;

namespace DnsClientX {
    /// <summary>Contains correlated query reports for one repeatable health profile run.</summary>
    public sealed class ResolverHealthReport {
        /// <summary>Gets or sets the profile name.</summary>
        public string ProfileName { get; set; } = string.Empty;
        /// <summary>Gets or sets when the run completed.</summary>
        public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        /// <summary>Gets or sets per-name/type probe reports, including response evidence and policies.</summary>
        public ResolverProbeReport[] Queries { get; set; } = Array.Empty<ResolverProbeReport>();
        /// <summary>Gets or sets the optional benchmark of the same catalog.</summary>
        public ResolverBenchmarkReport? Benchmark { get; set; }
        /// <summary>Gets or sets whether every requested policy passed.</summary>
        public bool PolicyPassed { get; set; }
        /// <summary>Gets or sets a factual answer-variation explanation.</summary>
        public string AnswerVariationNote { get; set; } = "Resolver differences can reflect location, caching, or split DNS policy. Agreement alone does not establish authoritative correctness.";
    }
}
