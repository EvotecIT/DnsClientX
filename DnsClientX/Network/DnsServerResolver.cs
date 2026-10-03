using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace DnsClientX {
    internal static class DnsServerResolver {
        private sealed class CacheEntry {
            internal CacheEntry(
                IPAddress? address,
                string? error,
                DateTimeOffset expiresAt,
                DateTimeOffset staleUntil,
                DateTimeOffset lastAccess,
                int failureCount, bool usedStaleAddress = false) {
                Address = address;
                Error = error;
                ExpiresAt = expiresAt;
                StaleUntil = staleUntil;
                LastAccess = lastAccess;
                FailureCount = failureCount;
                UsedStaleAddress = usedStaleAddress;
            }

            internal IPAddress? Address { get; }
            internal string? Error { get; }
            internal DateTimeOffset ExpiresAt { get; }
            internal DateTimeOffset StaleUntil { get; }
            internal DateTimeOffset LastAccess { get; }
            internal int FailureCount { get; }
            internal bool UsedStaleAddress { get; }
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
            TimeSpan? staleTtl = null,
            bool failureBackoffEnabled = false,
            double failureBackoffFactor = 2.0,
            TimeSpan? failureBackoffMaxTtl = null,
            AddressFamily? preferredAddressFamily = null,
            DnsResolverEndpoint? bootstrapResolver = null,
            Configuration? queryConfiguration = null) {
            if (string.IsNullOrWhiteSpace(dnsServer)) {
                return (null, "DNS server hostname is empty.");
            }

            if (IPAddress.TryParse(dnsServer, out var parsed)) {
                return Finish(parsed, null, false, false, null);
            }

            var now = DateTimeOffset.UtcNow;
            var successCacheTtl = successTtl ?? DefaultSuccessTtl;
            var failureCacheTtl = failureTtl ?? DefaultFailureTtl;
            var staleCacheTtl = staleTtl ?? DefaultStaleTtl;
            // Shared entries and flights may be reused only under equivalent caller policies.
            string cacheKey = FormattableString.Invariant($"{preferredAddressFamily?.ToString() ?? "Any"}|{dnsServer}|{timeoutMilliseconds}|{successCacheTtl.Ticks}|{failureCacheTtl.Ticks}|{allowStale}|{staleCacheTtl.Ticks}|{failureBackoffEnabled}|{failureBackoffFactor:R}|{failureBackoffMaxTtl?.Ticks}|{DnsBootstrapResolver.CacheKey(bootstrapResolver)}");
            CacheEntry? cached = null;
            var hasStale = false;

            if (Cache.TryGetValue(cacheKey, out cached) && cached != null) {
                if (cached.ExpiresAt > now) {
                    var refreshed = new CacheEntry(
                        cached.Address,
                        cached.Error,
                        cached.ExpiresAt,
                        cached.StaleUntil,
                        now,
                        cached.FailureCount);
                    Cache[cacheKey] = refreshed;
                    if (refreshed.Error != null) {
                        return allowStale && refreshed.Address != null && refreshed.StaleUntil > now
                            ? Finish(refreshed.Address, null, true, true, refreshed.Error) : Finish(null, refreshed.Error, true, false, refreshed.Error);
                    }
                    return Finish(refreshed.Address, null, true, false, null);
                }
                hasStale = allowStale && cached.Address != null && cached.StaleUntil > now;
            }

            var resolver = Inflight.GetOrAdd(
                cacheKey,
                _ => new Lazy<Task<CacheEntry>>(
                    () => ResolveAndCacheAsync(
                        dnsServer,
                        cacheKey,
                        timeoutMilliseconds,
                        successCacheTtl,
                        failureCacheTtl,
                        staleCacheTtl,
                        failureBackoffEnabled,
                        failureBackoffFactor,
                        failureBackoffMaxTtl,
                        preferredAddressFamily,
                        cached,
                        hasStale, bootstrapResolver),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            Task<CacheEntry> lookup = resolver.Value;
            _ = RemoveInflightWhenCompletedAsync(cacheKey, resolver, lookup);
            var entry = await WaitForTaskAsync(lookup, cancellationToken).ConfigureAwait(false);
            return Finish(entry.Address, entry.UsedStaleAddress ? null : entry.Error, false, entry.UsedStaleAddress, entry.Error);

            (IPAddress? Address, string? Error) Finish(IPAddress? address, string? error, bool fromCache, bool stale, string? diagnosticError) {
                if (queryConfiguration != null && !IPAddress.TryParse(dnsServer, out _)) {
                    queryConfiguration.ServerResolution = new DnsServerResolutionInfo(dnsServer, address?.ToString(),
                        bootstrapResolver == null ? null : $"{bootstrapResolver.Transport.ToString().ToLowerInvariant()}@{bootstrapResolver}", fromCache, stale, diagnosticError);
                }
                return (address, error);
            }
        }

        internal static Task<(IPAddress? Address, string? Error)> ResolveAsync(string dnsServer,
            Configuration configuration, CancellationToken cancellationToken) => ResolveAsync(dnsServer,
                configuration.TimeOut, cancellationToken, configuration.DnsServerResolutionSuccessTtl,
                configuration.DnsServerResolutionFailureTtl, configuration.DnsServerResolutionAllowStale,
                configuration.DnsServerResolutionStaleTtl, configuration.DnsServerResolutionFailureBackoffEnabled,
                configuration.DnsServerResolutionFailureBackoffFactor, configuration.DnsServerResolutionFailureBackoffMaxTtl,
                configuration.PreferredAddressFamily, configuration.BootstrapResolver, configuration);

        private static async Task RemoveInflightWhenCompletedAsync(string key, Lazy<Task<CacheEntry>> flight, Task<CacheEntry> task) {
            try { await task.ConfigureAwait(false); } catch { /* The waiting caller observes the failure. */ }
            ((ICollection<KeyValuePair<string, Lazy<Task<CacheEntry>>>>)Inflight).Remove(new(key, flight));
        }

        internal static void ResetForTests() {
            Cache.Clear();
            Inflight.Clear();
            ResolveHostAddressesAsync = DefaultResolver;
            MaxEntries = DefaultMaxEntries;
        }

        private static async Task<CacheEntry> ResolveAndCacheAsync(
            string dnsServer,
            string cacheKey,
            int timeoutMilliseconds,
            TimeSpan successCacheTtl,
            TimeSpan failureCacheTtl,
            TimeSpan staleCacheTtl,
            bool failureBackoffEnabled,
            double failureBackoffFactor,
            TimeSpan? failureBackoffMaxTtl,
            AddressFamily? preferredAddressFamily,
            CacheEntry? cached,
            bool hasStale, DnsResolverEndpoint? bootstrapResolver) {
            var now = DateTimeOffset.UtcNow;
            var failureCount = cached?.FailureCount ?? 0;
            try {
                Task<(IPAddress[] Addresses, TimeSpan? Ttl)> resolveTask = bootstrapResolver == null ? ResolveSystemAsync(dnsServer)
                    : DnsBootstrapResolver.ResolveAsync(dnsServer, bootstrapResolver, timeoutMilliseconds, preferredAddressFamily);
                using var timerCancellation = new CancellationTokenSource();
                if (timeoutMilliseconds > 0) {
                    Task delayTask = Task.Delay(timeoutMilliseconds, timerCancellation.Token);
                    Task completed = await Task.WhenAny(resolveTask, delayTask).ConfigureAwait(false);
                    if (completed != resolveTask) {
                        // System DNS cannot always be cancelled. Observe a late fault without retaining a waiter.
                        _ = resolveTask.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        var error = $"DNS server resolution timed out after {timeoutMilliseconds} milliseconds.";
                        failureCount++;
                        var failureExpiry = GetFailureExpiry(now, failureCacheTtl, failureBackoffEnabled, failureBackoffFactor, failureBackoffMaxTtl, failureCount);
                        if (hasStale && cached != null && cached.StaleUntil > DateTimeOffset.UtcNow) {
                            Cache[cacheKey] = new CacheEntry(cached.Address, error, failureExpiry, cached.StaleUntil, now, failureCount);
                            TrimCache(now);
                            return new CacheEntry(cached.Address, error, failureExpiry, cached.StaleUntil, now, failureCount, usedStaleAddress: true);
                        }
                        Cache[cacheKey] = new CacheEntry(null, error, failureExpiry, failureExpiry, now, failureCount);
                        TrimCache(now);
                        return new CacheEntry(null, error, failureExpiry, failureExpiry, now, failureCount);
                    }
                }
                timerCancellation.Cancel();
                var (addresses, wireTtl) = await resolveTask.ConfigureAwait(false);
                var usable = addresses.Where(IsUsableAddress).ToArray();
                if (usable.Length == 0) {
                    var error = $"No DNS addresses found for '{dnsServer}'.";
                    failureCount++;
                    var failureExpiry = GetFailureExpiry(now, failureCacheTtl, failureBackoffEnabled, failureBackoffFactor, failureBackoffMaxTtl, failureCount);
                    if (hasStale && cached != null && cached.StaleUntil > DateTimeOffset.UtcNow) {
                        Cache[cacheKey] = new CacheEntry(cached.Address, error, failureExpiry, cached.StaleUntil, now, failureCount);
                        TrimCache(now);
                        return new CacheEntry(cached.Address, error, failureExpiry, cached.StaleUntil, now, failureCount, usedStaleAddress: true);
                    }
                    Cache[cacheKey] = new CacheEntry(null, error, failureExpiry, failureExpiry, now, failureCount);
                    TrimCache(now);
                    return new CacheEntry(null, error, failureExpiry, failureExpiry, now, failureCount);
                }

                IPAddress? ipv4 = null;
                IPAddress? ipv6 = null;
                foreach (var address in usable) {
                    if (address.AddressFamily == AddressFamily.InterNetwork && ipv4 == null) {
                        ipv4 = address;
                    }
                    if (address.AddressFamily == AddressFamily.InterNetworkV6 && ipv6 == null) {
                        ipv6 = address;
                    }
                }

                var selected = preferredAddressFamily == AddressFamily.InterNetworkV6
                    ? ipv6 ?? ipv4 ?? usable[0]
                    : preferredAddressFamily == AddressFamily.InterNetwork
                        ? ipv4 ?? ipv6 ?? usable[0]
                        : ipv4 ?? ipv6 ?? usable[0];
                failureCount = 0;
                TimeSpan effectiveTtl = wireTtl.HasValue && wireTtl.Value < successCacheTtl ? wireTtl.Value : successCacheTtl;
                var entry = new CacheEntry(selected, null, now.Add(effectiveTtl), now.Add(staleCacheTtl), now, failureCount);
                Cache[cacheKey] = entry;
                TrimCache(now);
                return entry;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                failureCount++;
                var failureExpiry = GetFailureExpiry(now, failureCacheTtl, failureBackoffEnabled, failureBackoffFactor, failureBackoffMaxTtl, failureCount);
                if (hasStale && cached != null && cached.StaleUntil > DateTimeOffset.UtcNow) {
                    Cache[cacheKey] = new CacheEntry(cached.Address, ex.Message, failureExpiry, cached.StaleUntil, now, failureCount);
                    TrimCache(now);
                    return new CacheEntry(cached.Address, ex.Message, failureExpiry, cached.StaleUntil, now, failureCount, usedStaleAddress: true);
                }
                Cache[cacheKey] = new CacheEntry(null, ex.Message, failureExpiry, failureExpiry, now, failureCount);
                TrimCache(now);
                return new CacheEntry(null, ex.Message, failureExpiry, failureExpiry, now, failureCount);
            }
        }

        private static async Task<(IPAddress[] Addresses, TimeSpan? Ttl)> ResolveSystemAsync(string hostname) =>
            (await ResolveHostAddressesAsync(hostname).ConfigureAwait(false), null);

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

            foreach (var item in Cache.OrderBy(entry => entry.Value.LastAccess).Take(removeCount)) {
                Cache.TryRemove(item.Key, out _);
            }
        }

        private static DateTimeOffset GetFailureExpiry(
            DateTimeOffset now,
            TimeSpan failureCacheTtl,
            bool failureBackoffEnabled,
            double failureBackoffFactor,
            TimeSpan? failureBackoffMaxTtl,
            int failureCount) {
            if (!failureBackoffEnabled || failureCount <= 1) {
                return now.Add(failureCacheTtl);
            }

            var maxTtl = failureBackoffMaxTtl ?? TimeSpan.FromMinutes(5);
            var multiplier = Math.Pow(Math.Max(1.0, failureBackoffFactor), failureCount - 1);
            var scaledMs = failureCacheTtl.TotalMilliseconds * multiplier;
            var cappedMs = Math.Min(scaledMs, maxTtl.TotalMilliseconds);
            return now.Add(TimeSpan.FromMilliseconds(cappedMs));
        }

        private static bool IsUsableAddress(IPAddress address) {
            if (address == null) {
                return false;
            }

            if (IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address)) {
                return false;
            }

            if (IPAddress.None.Equals(address) || IPAddress.IPv6None.Equals(address)) {
                return false;
            }

            return true;
        }

        private static async Task<CacheEntry> WaitForTaskAsync(Task<CacheEntry> task, CancellationToken cancellationToken) {
            if (!cancellationToken.CanBeCanceled) return await task.ConfigureAwait(false);
            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
            if (await Task.WhenAny(task, canceled.Task).ConfigureAwait(false) != task) cancellationToken.ThrowIfCancellationRequested();
            return await task.ConfigureAwait(false);
        }
    }
}
