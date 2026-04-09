namespace DnsClientX {
    /// <summary>
    /// Describes policy gates applied to probe observations.
    /// </summary>
    public sealed class ResolverProbePolicy {
        /// <summary>
        /// Gets or sets the required minimum successful probe count, if any.
        /// </summary>
        public int? MinSuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the required minimum successful probe percentage, if any.
        /// </summary>
        public int? MinSuccessPercent { get; set; }

        /// <summary>
        /// Gets or sets the required minimum consensus percentage, if any.
        /// </summary>
        public int? MinConsensusPercent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether full answer consensus is required.
        /// </summary>
        public bool RequireConsensus { get; set; }
    }
}
