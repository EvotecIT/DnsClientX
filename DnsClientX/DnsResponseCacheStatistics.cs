namespace DnsClientX;

/// <summary>A snapshot of the process-wide response cache, shared by cache-enabled clients.</summary>
/// <remarks>
/// Counters accumulate for the lifetime of the process and are not reset by clearing entries.
/// Hits and misses count cache lookups, including internal single-flight checks, rather than queries.
/// </remarks>
public sealed class DnsResponseCacheStatistics {
    internal DnsResponseCacheStatistics(int capacity, int entryCount, long hits, long misses,
        long evictions, long expiredEntries) {
        Capacity = capacity;
        EntryCount = entryCount;
        Hits = hits;
        Misses = misses;
        Evictions = evictions;
        ExpiredEntries = expiredEntries;
    }

    /// <summary>Gets the maximum number of cached responses.</summary>
    public int Capacity { get; }
    /// <summary>Gets the current unexpired response count.</summary>
    public int EntryCount { get; }
    /// <summary>Gets successful cache lookup count.</summary>
    public long Hits { get; }
    /// <summary>Gets cache lookups that did not find an unexpired response.</summary>
    public long Misses { get; }
    /// <summary>Gets responses removed to enforce capacity.</summary>
    public long Evictions { get; }
    /// <summary>Gets responses removed after their lifetime expired.</summary>
    public long ExpiredEntries { get; }
}
