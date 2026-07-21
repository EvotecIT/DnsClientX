namespace DnsClientX {
    /// <summary>Controls parsing of BIND-compatible DNS master files.</summary>
    public sealed class DnsZoneFileParseOptions {
        /// <summary>Gets or sets the initial origin used for relative names.</summary>
        public string? Origin { get; set; }
        /// <summary>Gets or sets the initial default TTL in seconds.</summary>
        public int DefaultTtl { get; set; } = 3600;
        /// <summary>Gets or sets whether file parsing may process <c>$INCLUDE</c> directives.</summary>
        /// <remarks>Disabled by default because includes access the local filesystem.</remarks>
        public bool AllowIncludes { get; set; }
        /// <summary>
        /// Gets or sets the directory that relative <c>$INCLUDE</c> paths must remain within.
        /// When omitted, the directory containing the top-level zone file is used.
        /// </summary>
        public string? IncludeRootDirectory { get; set; }
        /// <summary>
        /// Gets or sets whether rooted or out-of-root <c>$INCLUDE</c> paths are allowed.
        /// Enable this only for trusted zone files.
        /// </summary>
        public bool AllowUnsafeIncludePaths { get; set; }
        /// <summary>Gets or sets the maximum nested <c>$INCLUDE</c> depth.</summary>
        public int MaxIncludeDepth { get; set; } = 16;
    }
}
