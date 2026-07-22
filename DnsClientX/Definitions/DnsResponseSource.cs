namespace DnsClientX {
    /// <summary>Identifies how a caller obtained a DNS response.</summary>
    public enum DnsResponseSource {
        /// <summary>The response was produced by a resolver query.</summary>
        Network,
        /// <summary>The response was cloned from the TTL-bounded response cache.</summary>
        Cache,
        /// <summary>The response joined an identical in-flight query instead of issuing another network request.</summary>
        CoalescedNetwork
    }
}
