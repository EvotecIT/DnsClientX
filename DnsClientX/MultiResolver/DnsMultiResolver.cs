using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Multi-endpoint resolver supporting FirstSuccess, FastestWins, and SequentialAll strategies.
    /// </summary>
    public sealed class DnsMultiResolver : IDnsMultiResolver {
        private readonly DnsResolverEndpoint[] _endpoints;
        private readonly MultiResolverOptions _options;

        private const int DefaultPerQueryTimeoutMs = Configuration.DefaultTimeout; // fallback when endpoint timeout not specified
        private int _roundRobinIndex = -1;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _endpointLimiters = new();

        // Simple metrics
        private static readonly ConcurrentDictionary<string, EndpointMetrics> Metrics = new();

        // Cache for FastestWins strategy
        private static readonly ConcurrentDictionary<string, FastestCacheEntry> FastestCache = new();

        private readonly struct FastestCacheEntry {
            public FastestCacheEntry(string key, DateTime expiresAt) {
                Key = key; ExpiresAt = expiresAt;
            }
            public string Key { get; }
            public DateTime ExpiresAt { get; }
        }

        private sealed class EndpointMetrics {
            public long SuccessCount;
            public long FailureCount;
            public TimeSpan LastRtt;
        }

        /// <summary>
        /// Creates a multi-endpoint resolver with provided endpoints and options.
        /// </summary>
        /// <param name="endpoints">One or more endpoints to target.</param>
        /// <param name="options">Behavioral options controlling strategy, parallelism, timeouts, and caching.</param>
        public DnsMultiResolver(IEnumerable<DnsResolverEndpoint> endpoints, MultiResolverOptions? options = null) {
            _endpoints = (endpoints ?? Array.Empty<DnsResolverEndpoint>()).ToArray();
            if (_endpoints.Length == 0) throw new ArgumentException("No endpoints provided", nameof(endpoints));
            _options = options ?? new MultiResolverOptions();
        }

        /// <summary>
        /// Queries a single name for the specified record type according to the configured strategy.
        /// </summary>
        public Task<DnsResponse> QueryAsync(string name, DnsRecordType type, CancellationToken ct = default) {
            return _options.Strategy switch {
                MultiResolverStrategy.FirstSuccess => QueryFirstSuccessAsync(name, type, ct),
                MultiResolverStrategy.FastestWins => QueryFastestWinsAsync(name, type, ct),
                MultiResolverStrategy.SequentialAll => QuerySequentialAsync(name, type, ct),
                MultiResolverStrategy.RoundRobin => QueryRoundRobinAsync(name, type, ct),
                _ => QueryFirstSuccessAsync(name, type, ct)
            };
        }

        /// <summary>
        /// Queries multiple names of the same record type, preserving input order, and isolating failures per-element.
        /// </summary>
        public async Task<DnsResponse[]> QueryBatchAsync(string[] names, DnsRecordType type, CancellationToken ct = default) {
            if (names == null || names.Length == 0) return Array.Empty<DnsResponse>();
            var results = new DnsResponse[names.Length];
            int maxPar = Math.Max(1, _options.MaxParallelism);
            using var sem = new SemaphoreSlim(maxPar, maxPar);
            var tasks = new List<Task>();
            for (int i = 0; i < names.Length; i++) {
                int idx = i;
                await sem.WaitAsync(ct).ConfigureAwait(false);
                tasks.Add(Task.Run(async () => {
                    try {
                        results[idx] = await QueryAsync(names[idx], type, ct).ConfigureAwait(false);
                    } finally {
                        sem.Release();
                    }
                }, ct));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return results;
        }

        private async Task<DnsResponse> QueryFirstSuccessAsync(string name, DnsRecordType type, CancellationToken ct) {
            // Chunk endpoints to respect MaxParallelism and cancel losers
            int batch = Math.Max(1, _options.MaxParallelism);
            var queue = new Queue<DnsResolverEndpoint>(_endpoints);
            using var globalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            List<Exception> exceptions = new();
            DnsResponse? bestError = null;

            while (queue.Count > 0) {
                var current = new List<Task<(DnsResponse resp, DnsResolverEndpoint ep)>>();
                for (int i = 0; i < batch && queue.Count > 0; i++) {
                    var ep = queue.Dequeue();
                    current.Add(InvokeEndpoint(ep, name, type, globalCts.Token));
                }

                while (current.Count > 0) {
                    var finished = await Task.WhenAny(current).ConfigureAwait(false);
                    current.Remove(finished);
                    (DnsResponse resp, DnsResolverEndpoint ep) = await finished.ConfigureAwait(false);
                    if (IsSuccess(resp)) {
                        globalCts.Cancel(); // cancel remaining tasks
                        return resp;
                    }
                    bestError = ChooseBetterError(bestError, resp);
                }
            }

            return bestError ?? MakeError(name, type, DnsQueryErrorCode.ServFail, "All endpoints failed");
        }

        private async Task<DnsResponse> QuerySequentialAsync(string name, DnsRecordType type, CancellationToken ct) {
            DnsResponse? bestError = null;
            foreach (var ep in _endpoints) {
                ct.ThrowIfCancellationRequested();
                var (resp, _) = await InvokeEndpoint(ep, name, type, ct).ConfigureAwait(false);
                if (IsSuccess(resp)) return resp;
                bestError = ChooseBetterError(bestError, resp);
            }
            return bestError ?? MakeError(name, type, DnsQueryErrorCode.ServFail, "All endpoints failed");
        }

        private async Task<DnsResponse> QueryFastestWinsAsync(string name, DnsRecordType type, CancellationToken ct) {
            string key = ComputeSetKey(_endpoints);
            if (_options.EnableFastestCache && FastestCache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow) {
                // The cache stores endpoint key - resolve to endpoint
                var ep = _endpoints.FirstOrDefault(e => EndpointKey(e) == cached.Key);
                if (ep != null) {
                    var (resp, _) = await InvokeEndpoint(ep, name, type, ct).ConfigureAwait(false);
                    if (IsSuccess(resp)) return resp;
                    // fallback to warm all below if cached failed
                }
            }

            // Warm all endpoints (bounded by parallelism) and pick the fastest success
            int batch = Math.Max(1, _options.MaxParallelism);
            var queue = new Queue<DnsResolverEndpoint>(_endpoints);
            using var globalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var inflight = new List<Task<(DnsResponse resp, DnsResolverEndpoint ep, TimeSpan rtt)>>();

            // prime first batch
            for (int i = 0; i < batch && queue.Count > 0; i++) {
                var ep = queue.Dequeue();
                inflight.Add(InvokeEndpointTimed(ep, name, type, globalCts.Token));
            }

            DnsResponse? best = null;
            DnsResolverEndpoint? bestEp = null;
            TimeSpan bestRtt = TimeSpan.MaxValue;
            DnsResponse? bestError = null;

            while (inflight.Count > 0) {
                var finished = await Task.WhenAny(inflight).ConfigureAwait(false);
                inflight.Remove(finished);
                var (resp, ep, rtt) = await finished.ConfigureAwait(false);
                if (IsSuccess(resp)) {
                    if (rtt < bestRtt) { best = resp; bestEp = ep; bestRtt = rtt; }
                } else {
                    bestError = ChooseBetterError(bestError, resp);
                }

                // launch next if available
                if (queue.Count > 0) {
                    var next = queue.Dequeue();
                    inflight.Add(InvokeEndpointTimed(next, name, type, globalCts.Token));
                }
            }

            if (best != null && bestEp != null) {
                string ek = EndpointKey(bestEp);
                if (_options.EnableFastestCache) {
                    FastestCache[key] = new FastestCacheEntry(ek, DateTime.UtcNow.Add(_options.FastestCacheDuration));
                }
                return best;
            }

            return bestError ?? MakeError(name, type, DnsQueryErrorCode.ServFail, "All endpoints failed");
        }

        private async Task<DnsResponse> QueryRoundRobinAsync(string name, DnsRecordType type, CancellationToken ct) {
            // Pick assigned endpoint by round-robin
            int idx = (System.Threading.Interlocked.Increment(ref _roundRobinIndex)) % _endpoints.Length;
            if (idx < 0) idx = -idx; // ensure non-negative
            var assigned = _endpoints[idx];
            var (resp, _) = await InvokeEndpoint(assigned, name, type, ct).ConfigureAwait(false);
            if (IsSuccess(resp)) return resp;

            // Fallback to first endpoint if different
            var first = _endpoints[0];
            if (!ReferenceEquals(first, assigned)) {
                var (fallback, _) = await InvokeEndpoint(first, name, type, ct).ConfigureAwait(false);
                if (IsSuccess(fallback)) return fallback;
                return ChooseBetterError(resp, fallback);
            }

            // If assigned was first, try second if available as a modest fallback
            if (_endpoints.Length > 1) {
                var second = _endpoints[1];
                var (fallback2, _) = await InvokeEndpoint(second, name, type, ct).ConfigureAwait(false);
                if (IsSuccess(fallback2)) return fallback2;
                return ChooseBetterError(resp, fallback2);
            }

            return resp;
        }

        private static bool IsSuccess(DnsResponse resp) {
            return resp != null && resp.Status == DnsResponseCode.NoError && string.IsNullOrEmpty(resp.Error);
        }

        private async Task<(DnsResponse resp, DnsResolverEndpoint ep)> InvokeEndpoint(DnsResolverEndpoint ep, string name, DnsRecordType type, CancellationToken ct) {
            var (resp, _, _) = await InvokeEndpointTimed(ep, name, type, ct).ConfigureAwait(false);
            return (resp, ep);
        }

        private async Task<(DnsResponse resp, DnsResolverEndpoint ep, TimeSpan rtt)> InvokeEndpointTimed(DnsResolverEndpoint ep, string name, DnsRecordType type, CancellationToken ct) {
            TimeSpan timeout = (_options.RespectEndpointTimeout && ep.Timeout.HasValue)
                ? ep.Timeout!.Value
                : (_options.DefaultTimeout ?? TimeSpan.FromMilliseconds(DefaultPerQueryTimeoutMs));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (timeout > TimeSpan.Zero) cts.CancelAfter(timeout);

            var sw = Stopwatch.StartNew();
            DnsResponse response;
            try {
                SemaphoreSlim? limiter = GetLimiter(ep);
                if (limiter != null) await limiter.WaitAsync(cts.Token).ConfigureAwait(false);
                try {
                    response = await PerformQuery(ep, name, type, cts.Token).ConfigureAwait(false);
                } finally {
                    if (limiter != null) limiter.Release();
                }
            } catch (OperationCanceledException oce) when (!ct.IsCancellationRequested) {
                response = MakeError(ep, name, type, DnsQueryErrorCode.Timeout, oce.Message, oce);
            } catch (SocketException se) {
                response = MakeError(ep, name, type, DnsQueryErrorCode.Network, se.Message, se);
            } catch (DnsClientException dce) {
                response = MakeError(ep, name, type, DnsQueryErrorCode.InvalidResponse, dce.Message, dce);
            } catch (Exception ex) {
                response = MakeError(ep, name, type, DnsQueryErrorCode.ServFail, ex.Message, ex);
            }
            sw.Stop();

            StampResponse(ep, response, sw.Elapsed);
            RecordMetrics(ep, response, sw.Elapsed);
            return (response, ep, sw.Elapsed);
        }

        private SemaphoreSlim? GetLimiter(DnsResolverEndpoint ep) {
            if (!_options.PerEndpointMaxInFlight.HasValue) return null;
            int cap = _options.PerEndpointMaxInFlight!.Value;
            if (cap <= 0) return null;
            string key = EndpointKey(ep);
            return _endpointLimiters.GetOrAdd(key, _ => new SemaphoreSlim(cap, cap));
        }

        private static void StampResponse(DnsResolverEndpoint ep, DnsResponse response, TimeSpan rtt) {
            if (response == null) return;
            response.UsedTransport = ep.Transport;
            response.UsedEndpoint = ep;
            response.RoundTripTime = rtt;
            response.ComputeTtlMetrics();
        }

        private void RecordMetrics(DnsResolverEndpoint ep, DnsResponse response, TimeSpan rtt) {
            var m = Metrics.GetOrAdd(EndpointKey(ep), _ => new EndpointMetrics());
            if (IsSuccess(response)) System.Threading.Interlocked.Increment(ref m.SuccessCount); else System.Threading.Interlocked.Increment(ref m.FailureCount);
            m.LastRtt = rtt;
        }

        private static string ComputeSetKey(IEnumerable<DnsResolverEndpoint> endpoints) => string.Join("|", endpoints.Select(EndpointKey));
        private static string EndpointKey(DnsResolverEndpoint ep) => ep.Transport + ":" + (ep.DohUrl?.ToString() ?? (ep.Host ?? string.Empty)) + ":" + ep.Port.ToString();

        private static DnsResponse ChooseBetterError(DnsResponse? current, DnsResponse candidate) {
            if (current == null) return candidate;
            int Rank(DnsResponse r) => r.ErrorCode switch {
                DnsQueryErrorCode.Timeout => 1,
                DnsQueryErrorCode.Network => 2,
                DnsQueryErrorCode.ServFail => 3,
                DnsQueryErrorCode.InvalidResponse => 4,
                _ => 0
            };
            return Rank(candidate) >= Rank(current) ? candidate : current;
        }

        private static DnsResponse MakeError(string name, DnsRecordType type, DnsQueryErrorCode code, string message) => new DnsResponse {
            Status = DnsResponseCode.ServerFailure,
            Questions = [ new DnsQuestion { Name = name, Type = type, OriginalName = name } ],
            Error = message,
            ErrorCode = code
        };

        private static DnsResponse MakeError(DnsResolverEndpoint ep, string name, DnsRecordType type, DnsQueryErrorCode code, string message, Exception? ex) {
            var r = MakeError(name, type, code, message);
            r.Exception = ex;
            r.UsedTransport = ep.Transport;
            r.UsedEndpoint = ep;
            return r;
        }

        /// <summary>
        /// Clears the in-memory FastestWins cache for all endpoint sets.
        /// </summary>
        public static void ClearFastestCache() => FastestCache.Clear();

        /// <summary>
        /// Clears the FastestWins cache for the provided set of endpoints only.
        /// </summary>
        public static void ClearFastestCacheFor(IEnumerable<DnsResolverEndpoint> endpoints) {
            if (endpoints == null) return;
            string key = ComputeSetKey(endpoints);
            FastestCache.TryRemove(key, out _);
        }

        private static DnsRequestFormat MapTransport(Transport t) => t switch {
            Transport.Udp => DnsRequestFormat.DnsOverUDP,
            Transport.Tcp => DnsRequestFormat.DnsOverTCP,
            Transport.Dot => DnsRequestFormat.DnsOverTLS,
            Transport.Doh => DnsRequestFormat.DnsOverHttps,
            _ => DnsRequestFormat.DnsOverUDP
        };

        private async Task<DnsResponse> PerformQuery(DnsResolverEndpoint ep, string name, DnsRecordType type, CancellationToken ct) {
            // Configure ClientX according to endpoint
            ClientX client;
            if (ep.Transport == Transport.Doh) {
                var dohUri = ep.DohUrl ?? new Uri($"https://{ep.Host}/dns-query");
                client = new ClientX(
                    baseUri: dohUri,
                    requestFormat: MapTransport(ep.Transport),
                    timeOutMilliseconds: DefaultPerQueryTimeoutMs,
                    userAgent: null,
                    httpVersion: null,
                    ignoreCertificateErrors: false,
                    enableCache: _options.EnableResponseCache,
                    useTcpFallback: true,
                    webProxy: null,
                    maxConnectionsPerServer: Configuration.DefaultMaxConnectionsPerServer);
            } else {
                if (string.IsNullOrWhiteSpace(ep.Host)) throw new ArgumentException("Endpoint.Host is required for non-DoH transports");
                client = new ClientX(
                    hostname: ep.Host!,
                    requestFormat: MapTransport(ep.Transport),
                    timeOutMilliseconds: DefaultPerQueryTimeoutMs,
                    userAgent: null,
                    httpVersion: null,
                    ignoreCertificateErrors: false,
                    enableCache: _options.EnableResponseCache,
                    useTcpFallback: true,
                    webProxy: null,
                    maxConnectionsPerServer: Configuration.DefaultMaxConnectionsPerServer);
            }

            // Fine-tune endpoint configuration
            if (ep.Transport != Transport.Doh) {
                client.EndpointConfiguration.Port = ep.Port > 0 ? ep.Port : (ep.Transport == Transport.Dot ? 853 : 53);
            } else {
                // DoH: keep 443 or URL port
                client.EndpointConfiguration.Port = (ep.DohUrl?.IsDefaultPort ?? true) ? 443 : ep.DohUrl!.Port;
            }
            client.EndpointConfiguration.UseTcpFallback = ep.AllowTcpFallback;
            if (ep.EdnsBufferSize.HasValue) client.EndpointConfiguration.UdpBufferSize = ep.EdnsBufferSize.Value;
            if (ep.DnsSecOk.HasValue) client.EndpointConfiguration.CheckingDisabled = !ep.DnsSecOk.Value;
            if (ep.Timeout.HasValue) client.EndpointConfiguration.TimeOut = (int)Math.Max(1, ep.Timeout.Value.TotalMilliseconds);

            // Configure caching bounds when enabled
            if (_options.EnableResponseCache) {
                if (_options.CacheExpiration.HasValue) client.CacheExpiration = _options.CacheExpiration.Value;
                if (_options.MinCacheTtl.HasValue) client.MinCacheTtl = _options.MinCacheTtl.Value;
                if (_options.MaxCacheTtl.HasValue) client.MaxCacheTtl = _options.MaxCacheTtl.Value;
            }

            // A single query; retries disabled, we rely on strategy behavior
            return await client.Resolve(name, type, requestDnsSec: ep.DnsSecOk ?? false, validateDnsSec: false, returnAllTypes: false, retryOnTransient: false, cancellationToken: ct).ConfigureAwait(false);
        }
    }
}
