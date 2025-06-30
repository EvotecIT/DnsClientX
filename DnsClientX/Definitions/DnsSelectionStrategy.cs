namespace DnsClientX {
    /// <summary>
    /// Defines how <see cref="DnsClientX"/> chooses between multiple DNS endpoints.
    /// This behavior is implementation specific and not defined by any RFC.
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
