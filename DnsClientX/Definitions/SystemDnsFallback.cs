namespace DnsClientX {
    /// <summary>
    /// Controls whether system DNS discovery may substitute public resolvers.
    /// </summary>
    public enum SystemDnsFallback {
        /// <summary>
        /// Do not substitute another resolver when the operating system exposes no DNS servers.
        /// </summary>
        None,

        /// <summary>
        /// Use Cloudflare and Google Public DNS when the operating system exposes no DNS servers.
        /// </summary>
        PublicResolvers
    }
}
