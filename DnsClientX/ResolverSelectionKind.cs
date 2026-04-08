namespace DnsClientX {
    /// <summary>
    /// Describes how a saved resolver recommendation should be interpreted.
    /// </summary>
    public enum ResolverSelectionKind {
        /// <summary>
        /// The recommendation points to a built-in resolver profile.
        /// </summary>
        BuiltInEndpoint,

        /// <summary>
        /// The recommendation points to an explicit resolver endpoint string.
        /// </summary>
        ExplicitEndpoint
    }
}
