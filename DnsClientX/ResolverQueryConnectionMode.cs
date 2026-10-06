namespace DnsClientX {
    /// <summary>Controls connection ownership during a resolver benchmark run.</summary>
    public enum ResolverQueryConnectionMode {
        /// <summary>Creates and disposes a client for every attempt, without reusing its connections.</summary>
        Cold,
        /// <summary>Reuses one client per target until the run finishes. DNS answer caching remains disabled.</summary>
        Warm,
        /// <summary>Reporting value for combined observations from different modes; not an execution option.</summary>
        Mixed
    }
}
