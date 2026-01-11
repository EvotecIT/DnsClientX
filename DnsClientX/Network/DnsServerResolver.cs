using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
        private static readonly TimeSpan DefaultSuccessTtl = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultFailureTtl = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan DefaultStaleTtl = TimeSpan.FromMinutes(10);
        private static readonly Func<string, Task<IPAddress[]>> DefaultResolver = Dns.GetHostAddressesAsync;

        internal static Func<string, Task<IPAddress[]>> ResolveHostAddressesAsync = DefaultResolver;

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

            try {
                Task<IPAddress[]> resolveTask = ResolveHostAddressesAsync(dnsServer);
                if (timeoutMilliseconds > 0) {
                    Task delayTask = Task.Delay(timeoutMilliseconds, cancellationToken);
                    Task completed = await Task.WhenAny(resolveTask, delayTask).ConfigureAwait(false);
                    if (completed != resolveTask) {
                        cancellationToken.ThrowIfCancellationRequested();
                        var error = $"DNS server resolution timed out after {timeoutMilliseconds} milliseconds.";
                        if (hasStale && cached != null) {
                            Cache[dnsServer] = new CacheEntry(cached.Address, error, now.Add(failureCacheTtl), now.Add(staleCacheTtl));
                            return (cached.Address, null);
                        }
                        Cache[dnsServer] = new CacheEntry(null, error, now.Add(failureCacheTtl), now.Add(failureCacheTtl));
                        return (null, error);
                    }
                }

                IPAddress[] addresses = await resolveTask.ConfigureAwait(false);
                if (addresses.Length == 0) {
                    var error = $"No DNS addresses found for '{dnsServer}'.";
                    if (hasStale && cached != null) {
                        Cache[dnsServer] = new CacheEntry(cached.Address, error, now.Add(failureCacheTtl), now.Add(staleCacheTtl));
                        return (cached.Address, null);
                    }
                    Cache[dnsServer] = new CacheEntry(null, error, now.Add(failureCacheTtl), now.Add(failureCacheTtl));
                    return (null, error);
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
                Cache[dnsServer] = new CacheEntry(selected, null, now.Add(successCacheTtl), now.Add(staleCacheTtl));
                return (selected, null);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                if (hasStale && cached != null) {
                    Cache[dnsServer] = new CacheEntry(cached.Address, ex.Message, now.Add(failureCacheTtl), now.Add(staleCacheTtl));
                    return (cached.Address, null);
                }
                Cache[dnsServer] = new CacheEntry(null, ex.Message, now.Add(failureCacheTtl), now.Add(failureCacheTtl));
                return (null, ex.Message);
            }
        }

        internal static void ResetForTests() {
            Cache.Clear();
            ResolveHostAddressesAsync = DefaultResolver;
        }
    }
}
