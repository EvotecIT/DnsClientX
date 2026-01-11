using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace DnsClientX {
    internal static class DnsServerResolver {
        private sealed class CacheEntry {
            internal CacheEntry(IPAddress? address, string? error, DateTimeOffset expiresAt, DateTimeOffset staleUntil) {
                Address = address;
                Error = error;
                ExpiresAt = expiresAt;
                StaleUntil = staleUntil;
            }

            internal IPAddress? Address { get; }
            internal string? Error { get; }
            internal DateTimeOffset ExpiresAt { get; }
            internal DateTimeOffset StaleUntil { get; }
        }

        private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Lazy<Task<CacheEntry>>> Inflight = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan DefaultSuccessTtl = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultFailureTtl = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan DefaultStaleTtl = TimeSpan.FromMinutes(10);
        private static readonly int DefaultMaxEntries = 4096;
        private static readonly Func<string, Task<IPAddress[]>> DefaultResolver = Dns.GetHostAddressesAsync;

        internal static Func<string, Task<IPAddress[]>> ResolveHostAddressesAsync = DefaultResolver;
        internal static int MaxEntries = DefaultMaxEntries;

        internal static async Task<(IPAddress? Address, string? Error)> ResolveAsync(
            string dnsServer,
            int timeoutMilliseconds,
            CancellationToken cancellationToken,
            TimeSpan? successTtl = null,
            TimeSpan? failureTtl = null,
            bool allowStale = true,
            TimeSpan? staleTtl = null) {
            if (string.IsNullOrWhiteSpace(dnsServer)) {
                return (null, "DNS server hostname is empty.");
            }

            if (IPAddress.TryParse(dnsServer, out var parsed)) {
                return (parsed, null);
            }

            var now = DateTimeOffset.UtcNow;
            var successCacheTtl = successTtl ?? DefaultSuccessTtl;
            var failureCacheTtl = failureTtl ?? DefaultFailureTtl;
            var staleCacheTtl = staleTtl ?? DefaultStaleTtl;
            CacheEntry? cached = null;
            var hasStale = false;

            if (Cache.TryGetValue(dnsServer, out cached) && cached != null) {
                if (cached.ExpiresAt > now) {
                    return (cached.Address, cached.Error);
                }
                hasStale = allowStale && cached.Address != null && cached.StaleUntil > now;
            }

            var resolver = Inflight.GetOrAdd(
                dnsServer,
                _ => new Lazy<Task<CacheEntry>>(
                    () => ResolveAndCacheAsync(
                        dnsServer,
                        timeoutMilliseconds,
                        successCacheTtl,
                        failureCacheTtl,
                        staleCacheTtl,
                        cached,
                        hasStale),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            try {
                var entry = await WaitForTaskAsync(resolver.Value, cancellationToken).ConfigureAwait(false);
                return (entry.Address, entry.Error);
            } finally {
                if (resolver.IsValueCreated && resolver.Value.IsCompleted) {
                    Inflight.TryRemove(dnsServer, out _);
                }
            }
        }

        internal static void ResetForTests() {
            Cache.Clear();
            Inflight.Clear();
            ResolveHostAddressesAsync = DefaultResolver;
            MaxEntries = DefaultMaxEntries;
        }

        private static async Task<CacheEntry> ResolveAndCacheAsync(
            string dnsServer,
            int timeoutMilliseconds,
            TimeSpan successCacheTtl,
            TimeSpan failureCacheTtl,
            TimeSpan staleCacheTtl,
            CacheEntry? cached,
            bool hasStale) {
            var now = DateTimeOffset.UtcNow;
            try {
                Task<IPAddress[]> resolveTask = ResolveHostAddressesAsync(dnsServer);
                if (timeoutMilliseconds > 0) {
                    Task delayTask = Task.Delay(timeoutMilliseconds);
                    Task completed = await Task.WhenAny(resolveTask, delayTask).ConfigureAwait(false);
                    if (completed != resolveTask) {
                        var error = $"DNS server resolution timed out after {timeoutMilliseconds} milliseconds.";
                        if (hasStale && cached != null) {
                            Cache[dnsServer] = new CacheEntry(cached.Address, error, now.Add(failureCacheTtl), now.Add(staleCacheTtl));
                            TrimCache(now);
                            return new CacheEntry(cached.Address, null, now.Add(failureCacheTtl), now.Add(staleCacheTtl));
                        }
                        Cache[dnsServer] = new CacheEntry(null, error, now.Add(failureCacheTtl), now.Add(failureCacheTtl));
                        TrimCache(now);
                        return new CacheEntry(null, error, now.Add(failureCacheTtl), now.Add(failureCacheTtl));
                    }
                }

                IPAddress[] addresses = await resolveTask.ConfigureAwait(false);
                if (addresses.Length == 0) {
                    var error = $"No DNS addresses found for '{dnsServer}'.";
                    if (hasStale && cached != null) {
                        Cache[dnsServer] = new CacheEntry(cached.Address, error, now.Add(failureCacheTtl), now.Add(staleCacheTtl));
                        TrimCache(now);
                        return new CacheEntry(cached.Address, null, now.Add(failureCacheTtl), now.Add(staleCacheTtl));
                    }
                    Cache[dnsServer] = new CacheEntry(null, error, now.Add(failureCacheTtl), now.Add(failureCacheTtl));
                    TrimCache(now);
                    return new CacheEntry(null, error, now.Add(failureCacheTtl), now.Add(failureCacheTtl));
                }

                IPAddress? ipv4 = null;
                IPAddress? ipv6 = null;
                foreach (var address in addresses) {
                    if (address.AddressFamily == AddressFamily.InterNetwork) {
                        ipv4 = address;
                        break;
                    }
                    if (address.AddressFamily == AddressFamily.InterNetworkV6 && ipv6 == null) {
                        ipv6 = address;
                    }
                }

                var selected = ipv4 ?? ipv6 ?? addresses[0];
                var entry = new CacheEntry(selected, null, now.Add(successCacheTtl), now.Add(staleCacheTtl));
                Cache[dnsServer] = entry;
                TrimCache(now);
                return entry;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                if (hasStale && cached != null) {
                    Cache[dnsServer] = new CacheEntry(cached.Address, ex.Message, now.Add(failureCacheTtl), now.Add(staleCacheTtl));
                    TrimCache(now);
                    return new CacheEntry(cached.Address, null, now.Add(failureCacheTtl), now.Add(staleCacheTtl));
                }
                Cache[dnsServer] = new CacheEntry(null, ex.Message, now.Add(failureCacheTtl), now.Add(failureCacheTtl));
                TrimCache(now);
                return new CacheEntry(null, ex.Message, now.Add(failureCacheTtl), now.Add(failureCacheTtl));
            }
        }

        private static void TrimCache(DateTimeOffset now) {
            if (Cache.Count <= MaxEntries) {
                return;
            }

            foreach (var item in Cache) {
                if (item.Value.StaleUntil <= now) {
                    Cache.TryRemove(item.Key, out _);
                }
            }

            if (Cache.Count <= MaxEntries) {
                return;
            }

            int removeCount = Cache.Count - MaxEntries;
            if (removeCount <= 0) {
                return;
            }

            foreach (var item in Cache.OrderBy(entry => entry.Value.ExpiresAt).Take(removeCount)) {
                Cache.TryRemove(item.Key, out _);
            }
        }

        private static async Task<CacheEntry> WaitForTaskAsync(Task<CacheEntry> task, CancellationToken cancellationToken) {
            if (!cancellationToken.CanBeCanceled) {
                return await task.ConfigureAwait(false);
            }

            Task cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
            Task completed = await Task.WhenAny(task, cancelTask).ConfigureAwait(false);
            if (completed != task) {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return await task.ConfigureAwait(false);
        }
    }
}
