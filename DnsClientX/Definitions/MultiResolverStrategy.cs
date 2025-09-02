namespace DnsClientX {
    /// <summary>
    /// Strategy for choosing among multiple resolver endpoints.
    /// </summary>
    public enum MultiResolverStrategy {
        /// <summary>
        /// Race endpoints (bounded by MaxParallelism) and return the first successful response, cancelling the rest.
        /// </summary>
        FirstSuccess,
        /// <summary>
        /// Warm all endpoints and prefer the endpoint that produced the fastest successful response, caching that choice for a duration.
        /// </summary>
        FastestWins,
        /// <summary>
        /// Try endpoints sequentially and return the first success or the best error if all fail.
        /// </summary>
        SequentialAll
        ,
        /// <summary>
        /// Distribute queries across endpoints in round-robin fashion to balance load. The
        /// chosen endpoint handles the query; on failure, the resolver falls back to the first
        /// endpoint (or the second if the first was chosen). Combine with <see cref="MultiResolverOptions.MaxParallelism"/>
        /// to cap overall concurrency and <see cref="MultiResolverOptions.PerEndpointMaxInFlight"/>
        /// to limit per-endpoint concurrency.
        /// </summary>
        RoundRobin
    }
}
