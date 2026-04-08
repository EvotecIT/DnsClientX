using System;

namespace DnsClientX {
    /// <summary>
    /// Represents one resolver candidate observation from a probe workflow.
    /// </summary>
    public sealed class ResolverProbeObservation {
        /// <summary>
        /// Gets or sets the human-readable candidate label.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolver address observed for the candidate.
        /// </summary>
        public string Resolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the observed or inferred transport for the candidate.
        /// </summary>
        public string Transport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the elapsed probe time in milliseconds.
        /// </summary>
        public double ElapsedMs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the probe succeeded.
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Gets or sets the stable answer signature used for consensus grouping.
        /// </summary>
        public string AnswerSignature { get; set; } = "(no answers)";
    }
}
