namespace DnsClientX {
    /// <summary>
    /// Describes one distinct successful probe answer set and its responders.
    /// </summary>
    public sealed class ResolverProbeAnswerVariant {
        /// <summary>
        /// Gets or sets the answer set description.
        /// </summary>
        public string AnswerSet { get; set; } = "(no answers)";

        /// <summary>
        /// Gets or sets the targets that produced the answer set.
        /// </summary>
        public string[] Targets { get; set; } = System.Array.Empty<string>();
    }
}
