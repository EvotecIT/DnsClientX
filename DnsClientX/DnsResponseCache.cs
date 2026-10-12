using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Collections.Generic;

namespace DnsClientX {
    /// <summary>Stores independent response snapshots with bounded lifetimes and storage.</summary>
    internal sealed class DnsResponseCache : IDisposable {
        private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
        private readonly object _gate = new();
        private readonly Timer _cleanupTimer;
        private int _capacity;
        private readonly LinkedList<string> _insertionOrder = new();
        private long _hits, _misses, _evictions, _expiredEntries;
        private bool _disposed;

        /// <summary>Creates a cache with periodic expired-entry removal and a positive capacity.</summary>
        public DnsResponseCache(TimeSpan? cleanupInterval = null, int cleanupThreshold = 1000) {
            if (cleanupThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(cleanupThreshold));
            _capacity = cleanupThreshold;
            TimeSpan interval = cleanupInterval ?? TimeSpan.FromMinutes(5);
            _cleanupTimer = new Timer(_ => Cleanup(), null, interval, interval);
        }

        private sealed class CacheEntry {
            internal CacheEntry(DnsResponse response, DateTimeOffset expiration, long insertedTimestamp,
                TimeSpan lifetime, LinkedListNode<string> node, string? name, DnsRecordType? type) {
                Response = response;
                Expiration = expiration;
                InsertedTimestamp = insertedTimestamp;
                Lifetime = lifetime;
                Node = node;
                Name = name;
                Type = type;
            }
            internal DnsResponse Response { get; }
            public DateTimeOffset Expiration { get; }
            internal long InsertedTimestamp { get; }
            internal TimeSpan Lifetime { get; }
            internal LinkedListNode<string> Node { get; }
            internal string? Name { get; }
            internal DnsRecordType? Type { get; }
            internal double ElapsedSeconds(long timestamp) =>
                Math.Max(0, (timestamp - InsertedTimestamp) / (double)Stopwatch.Frequency);
            internal bool IsExpired(DateTimeOffset utcNow, long timestamp) =>
                utcNow >= Expiration || ElapsedSeconds(timestamp) >= Lifetime.TotalSeconds;
        }

        internal int Capacity {
            get { lock (_gate) return _capacity; }
            set {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Cache capacity must be positive.");
                lock (_gate) {
                    _capacity = value;
                    CleanupExpired(DateTimeOffset.UtcNow, Stopwatch.GetTimestamp());
                    TrimToCapacity();
                }
            }
        }

        /// <summary>Returns an aged independent response, preserving original wire evidence.</summary>
        public bool TryGet(string key, [NotNullWhen(true)] out DnsResponse? response) {
            CacheEntry? found = null;
            long timestamp = Stopwatch.GetTimestamp();
            lock (_gate) {
                if (_cache.TryGetValue(key, out var entry)) {
                    if (!entry.IsExpired(DateTimeOffset.UtcNow, timestamp)) found = entry;
                    else {
                        Remove(entry.Node);
                        _expiredEntries++;
                    }
                }
                if (found == null) _misses++;
                else _hits++;
            }
            if (found != null) {
                response = found.Response.Clone();
                int elapsedSeconds = (int)Math.Min(int.MaxValue, Math.Floor(found.ElapsedSeconds(timestamp)));
                AgeRecords(response.Answers, elapsedSeconds);
                AgeRecords(response.Authorities, elapsedSeconds);
                AgeRecords(response.Additional, elapsedSeconds);
                response.RefreshDerivedData();
                return true;
            }
            response = null;
            return false;
        }

        private static void AgeRecords(DnsAnswer[]? records, int elapsedSeconds) {
            if (records == null) return;
            for (int index = 0; index < records.Length; index++) {
                // OPT uses its wire TTL field for EDNS flags/version, not a record lifetime.
                if (records[index].Type != DnsRecordType.OPT) {
                    records[index].TTL = Math.Max(0, records[index].TTL) > elapsedSeconds
                        ? records[index].TTL - elapsedSeconds : 0;
                }
            }
        }

        /// <summary>Removes expired entries and their eviction bookkeeping.</summary>
        public void Cleanup() {
            lock (_gate) CleanupExpired(DateTimeOffset.UtcNow, Stopwatch.GetTimestamp());
        }

        private void CleanupExpired(DateTimeOffset utcNow, long timestamp) {
            var node = _insertionOrder.First;
            while (node != null) {
                var next = node.Next;
                if (_cache[node.Value].IsExpired(utcNow, timestamp)) {
                    Remove(node);
                    _expiredEntries++;
                }
                node = next;
            }
        }

        /// <summary>Stores a snapshot; authentication verdicts cannot outlive their proofs.</summary>
        public void Set(string key, DnsResponse response, TimeSpan ttl, string? queryName = null,
            DnsRecordType? queryType = null) {
            if (ttl <= TimeSpan.Zero) return;
            var now = DateTimeOffset.UtcNow;
            long timestamp = Stopwatch.GetTimestamp();
            var expiration = now.Add(ttl);
            if (response.DnsSecValidationAttempted) {
                if (response.DnsSecValidationStatus != DnsSecValidationStatus.Secure
                    && response.DnsSecValidationStatus != DnsSecValidationStatus.Insecure) return;
                if (!response.DnsSecValidationExpiresUtc.HasValue) return;
                if (response.DnsSecValidationExpiresUtc.Value < expiration) {
                    expiration = response.DnsSecValidationExpiresUtc.Value;
                }
                if (expiration <= now) return;
            }
            DnsResponse snapshot = response.Clone();
            if (queryName == null && response.Questions is { Length: > 0 }) {
                queryName = response.Questions[0].Name;
                queryType ??= response.Questions[0].Type;
            }
            string? canonicalName = queryName == null ? null : DnsWireNameCodec.Canonical(queryName);
            lock (_gate) {
                var node = _cache.TryGetValue(key, out var previous)
                    ? previous.Node : _insertionOrder.AddLast(key);
                _cache[key] = new CacheEntry(snapshot, expiration, timestamp,
                    expiration - now, node, canonicalName, queryType);
                if (_cache.Count > _capacity) {
                    CleanupExpired(now, timestamp);
                    TrimToCapacity();
                }
            }
        }

        private void TrimToCapacity() {
            while (_cache.Count > _capacity && _insertionOrder.First != null) {
                Remove(_insertionOrder.First);
                _evictions++;
            }
        }

        /// <summary>Clears entries without resetting lifetime lookup and removal statistics.</summary>
        internal int Clear() {
            lock (_gate) {
                int removed = _cache.Count;
                _cache.Clear();
                _insertionOrder.Clear();
                return removed;
            }
        }

        internal int Invalidate(string canonicalName, DnsRecordType? type) {
            lock (_gate) {
                int removed = 0;
                var node = _insertionOrder.First;
                while (node != null) {
                    var next = node.Next;
                    CacheEntry entry = _cache[node.Value];
                    if (entry.Name == canonicalName && (!type.HasValue || entry.Type == type)) {
                        Remove(node);
                        removed++;
                    }
                    node = next;
                }
                return removed;
            }
        }

        internal DnsResponseCacheStatistics GetStatistics() {
            lock (_gate) {
                CleanupExpired(DateTimeOffset.UtcNow, Stopwatch.GetTimestamp());
                return new DnsResponseCacheStatistics(_capacity, _cache.Count, _hits, _misses,
                    _evictions, _expiredEntries);
            }
        }

        private void Remove(LinkedListNode<string> node) {
            _cache.Remove(node.Value);
            _insertionOrder.Remove(node);
        }

        /// <inheritdoc/>
        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            _cleanupTimer.Dispose();
        }
    }
}
