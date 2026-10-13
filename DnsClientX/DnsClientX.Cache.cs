using System;

namespace DnsClientX;

public partial class ClientX {
    /// <summary>Gets or sets the positive capacity of the process-wide response cache.</summary>
    /// <remarks>
    /// Applies to all cache-enabled clients. Reducing capacity immediately removes the oldest
    /// inserted entries. Per-client <see cref="MaxCacheTtl"/> and security policies still separate entries.
    /// </remarks>
    public static int ResponseCacheCapacity {
        get => _cache.Capacity;
        set => _cache.Capacity = value;
    }

    /// <summary>Returns cache storage and lifetime lookup statistics for all clients in this process.</summary>
    public static DnsResponseCacheStatistics GetResponseCacheStatistics() => _cache.GetStatistics();

    /// <summary>Clears all process-wide cached responses and returns the number removed.</summary>
    /// <remarks>Does not cancel in-flight queries; those may populate the cache when they complete.</remarks>
    public static int ClearResponseCache() => _cache.Clear();

    /// <summary>Invalidates cached queries for a name and optional record type across all resolver policies.</summary>
    /// <param name="name">Actual queried name, including a full system-search candidate when applicable.</param>
    /// <param name="type">Query type to remove, or null to remove all types for the name.</param>
    /// <returns>Number of entries removed.</returns>
    /// <remarks>
    /// Names are case-insensitive; Unicode names and a trailing root dot normalize to the same
    /// cache identity. Pass a reverse DNS name to invalidate an address lookup made with PTR.
    /// Alias targets do not invalidate a different original query. In-flight queries are unaffected.
    /// </remarks>
    public static int InvalidateResponseCache(string name, DnsRecordType? type = null) {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        return _cache.Invalidate(DnsWireNameCodec.Canonical(ConvertToPunycode(name)), type);
    }
}
