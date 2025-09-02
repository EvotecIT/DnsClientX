using System;

namespace DnsClientX {
    /// <summary>
    /// Options controlling behavior of <see cref="DnsMultiResolver"/>.
    /// </summary>
    public sealed class MultiResolverOptions {
        private int _maxParallelism = 4;
        private int? _perEndpointMaxInFlight;
        /// <summary>
        /// Strategy used when multiple endpoints are configured.
        /// </summary>
        public MultiResolverStrategy Strategy { get; set; } = MultiResolverStrategy.FirstSuccess;

        /// <summary>
        /// Upper bound on the number of endpoints queried concurrently.
        /// Applies to FirstSuccess and FastestWins warm-up.
        /// </summary>
        public int MaxParallelism {
            get => _maxParallelism;
            set => _maxParallelism = value <= 0 ? 1 : value;
        }

        /// <summary>
        /// Prefer IPv6 when resolving hostnames (when applicable).
        /// </summary>
        public bool PreferIpv6 { get; set; }

        /// <summary>
        /// Respect per-endpoint timeout value if specified; otherwise use <see cref="DefaultTimeout"/>.
        /// </summary>
        public bool RespectEndpointTimeout { get; set; } = true;

        /// <summary>
        /// Fallback per-query timeout used when an endpoint doesn't provide one.
        /// When null, uses <see cref="Configuration.DefaultTimeout"/>.
        /// </summary>
        public TimeSpan? DefaultTimeout { get; set; }

        /// <summary>
        /// Cache duration for FastestWins fastest-endpoint selection.
        /// </summary>
        public TimeSpan FastestCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Enables or disables the FastestWins cache.
        /// </summary>
        public bool EnableFastestCache { get; set; } = true;

        /// <summary>
        /// Optional cap on concurrent queries per endpoint. When set to a positive value,
        /// the resolver limits the number of in-flight queries against any single endpoint
        /// to this value. When null or less than or equal to zero, no per-endpoint cap is applied.
        /// </summary>
        public int? PerEndpointMaxInFlight {
            get => _perEndpointMaxInFlight;
            set {
                if (value.HasValue && value.Value <= 0) {
                    _perEndpointMaxInFlight = null;
                } else {
                    _perEndpointMaxInFlight = value;
                }
            }
        }

        /// <summary>
        /// Enables response caching based on record TTLs. When enabled, the resolver leverages the
        /// library's built-in cache to avoid repeated lookups for the same (name,type) across instances.
        /// </summary>
        public bool EnableResponseCache { get; set; }

        /// <summary>
        /// Default cache expiration used when TTL is unavailable. When null, uses the library default.
        /// </summary>
        public TimeSpan? CacheExpiration { get; set; }

        /// <summary>
        /// Minimal TTL allowed for cached entries. Entries with smaller TTLs are rounded up to this value.
        /// </summary>
        public TimeSpan? MinCacheTtl { get; set; }

        /// <summary>
        /// Maximal TTL allowed for cached entries. Entries with larger TTLs are clipped to this value.
        /// </summary>
        public TimeSpan? MaxCacheTtl { get; set; }
    }
}
