namespace DnsClientX {
    /// <summary>
    /// Defines the strategy for selecting a DNS server.
    /// </summary>
    public enum DnsSelectionStrategy {
        /// <summary>
        /// First DNS server in the list.
        /// </summary>
        First,
        /// <summary>
        /// Randomly select a DNS server.
        /// </summary>
        Random,
        /// <summary>
        /// Failover to the next DNS server if the current one fails.
        /// </summary>
        Failover
    }
}
