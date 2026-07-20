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
    /// Multi-endpoint resolver supporting racing, sequential fallback, round-robin, and random strategies.
    /// </summary>
    public sealed class DnsMultiResolver : IDnsMultiResolver, IDisposable {
        internal static Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<DnsResponse>>? ResolveOverride;
        internal static Func<int, int>? RandomIndexOverride;
        private readonly DnsResolverEndpoint[] _endpoints;
        private readonly MultiResolverOptions _options;

        private const int DefaultPerQueryTimeoutMs = Configuration.DefaultTimeout; // fallback when endpoint timeout not specified
        private int _roundRobinIndex = -1;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _endpointLimiters = new();
        private readonly ConcurrentDictionary<string, Lazy<ClientX>> _clients = new();
        private readonly SemaphoreSlim _globalLimiter;
        private readonly string _endpointSetKey;

        // Cache for FastestWins strategy
        private static readonly ConcurrentDictionary<string, FastestCacheEntry> FastestCache = new();
        private static readonly object FastestCacheGate = new();
        private const int FastestCacheCapacity = 256;
        private int _disposed;

        private readonly struct FastestCacheEntry {
            public FastestCacheEntry(string key, DateTime expiresAt) {
                Key = key; ExpiresAt = expiresAt;
            }
            public string Key { get; }
            public DateTime ExpiresAt { get; }
        }

        /// <summary>
        /// Creates a multi-endpoint resolver with provided endpoints and options.
        /// </summary>
        /// <param name="endpoints">One or more endpoints to target.</param>
        /// <param name="options">Behavioral options controlling strategy, parallelism, timeouts, and caching.</param>
        public DnsMultiResolver(IEnumerable<DnsResolverEndpoint> endpoints, MultiResolverOptions? options = null) {
            _endpoints = (endpoints ?? Array.Empty<DnsResolverEndpoint>()).ToArray();
            if (_endpoints.Length == 0) throw new ArgumentException("No endpoints provided", nameof(endpoints));
            _options = (options ?? new MultiResolverOptions()).Clone();
            _globalLimiter = new SemaphoreSlim(_options.MaxParallelism, _options.MaxParallelism);
            _endpointSetKey = ComputeSetKey(_endpoints);

            // Validate endpoints early to fail-fast on obvious misconfiguration
            foreach (var ep in _endpoints) {
                if (ep == null) throw new ArgumentException("Endpoint cannot be null.", nameof(endpoints));
                if (ep.Transport == Transport.Doh) {
                    if (ep.DohUrl == null || !string.Equals(ep.DohUrl.Scheme, "https", StringComparison.OrdinalIgnoreCase)) {
                        throw new ArgumentException($"Invalid DoH endpoint: {ep}. HTTPS URL is required.", nameof(endpoints));
                    }
                    // Validate custom DoH port when specified
                    if (!ep.DohUrl.IsDefaultPort) {
                        int p = ep.DohUrl.Port;
                        if (p <= 0 || p > 65535) {
                            throw new ArgumentOutOfRangeException(nameof(ep.DohUrl), p, "DoH URL port must be between 1 and 65535.");
                        }
                    }
                } else {
                    if (string.IsNullOrWhiteSpace(ep.Host)) {
                        throw new ArgumentException("Endpoint Host is required for non-DoH transports.", nameof(endpoints));
                    }
                    if (ep.Port <= 0 || ep.Port > 65535) {
                        throw new ArgumentOutOfRangeException(nameof(ep.Port), ep.Port, "Port must be between 1 and 65535.");
                    }
                }
            }
        }

        /// <summary>
        /// Queries a single name for the specified record type according to the configured strategy.
        /// </summary>
        public Task<DnsResponse> QueryAsync(string name, DnsRecordType type, CancellationToken ct = default) {
            ThrowIfDisposed();
            return _options.Strategy switch {
                MultiResolverStrategy.FirstSuccess => QueryFirstSuccessAsync(name, type, ct),
                MultiResolverStrategy.FastestWins => QueryFastestWinsAsync(name, type, ct),
                MultiResolverStrategy.SequentialFallback => QuerySequentialAsync(name, type, ct),
                MultiResolverStrategy.RoundRobin => QueryRoundRobinAsync(name, type, ct),
                MultiResolverStrategy.Random => QueryRandomAsync(name, type, ct),
                _ => QueryFirstSuccessAsync(name, type, ct)
            };
        }

        /// <summary>
        /// Queries every configured endpoint, preserving endpoint order and the configured global concurrency bound.
        /// Transport failures are represented as error responses so one unavailable endpoint does not discard the others.
        /// </summary>
        public async Task<DnsResponse[]> QueryAllAsync(string name, DnsRecordType type, CancellationToken ct = default) {
            ThrowIfDisposed();
            var results = new DnsResponse[_endpoints.Length];
            int nextIndex = -1;
            int workerCount = Math.Min(_endpoints.Length, Math.Max(1, _options.MaxParallelism));

            async Task RunWorkerAsync() {
                while (true) {
                    int idx = Interlocked.Increment(ref nextIndex);
                    if (idx >= _endpoints.Length) return;
                    (results[idx], _) = await InvokeEndpoint(_endpoints[idx], name, type, ct).ConfigureAwait(false);
                }
            }

            var workers = new Task[workerCount];
            for (int i = 0; i < workerCount; i++) {
                workers[i] = RunWorkerAsync();
            }
            await Task.WhenAll(workers).ConfigureAwait(false);
            return results;
        }

        /// <summary>
        /// Queries multiple names of the same record type, preserving input order, and isolating failures per-element.
        /// </summary>
        public async Task<DnsResponse[]> QueryBatchAsync(string[] names, DnsRecordType type, CancellationToken ct = default) {
            ThrowIfDisposed();
            if (names == null || names.Length == 0) return Array.Empty<DnsResponse>();
            var results = new DnsResponse[names.Length];
            int nextIndex = -1;
            int workerCount = Math.Min(names.Length, Math.Max(1, _options.MaxParallelism));

            async Task RunWorkerAsync() {
                while (true) {
                    int idx = Interlocked.Increment(ref nextIndex);
                    if (idx >= names.Length) return;
                    results[idx] = await QueryAsync(names[idx], type, ct).ConfigureAwait(false);
                }
            }

            var workers = new Task[workerCount];
            for (int i = 0; i < workerCount; i++) {
                workers[i] = RunWorkerAsync();
            }
            await Task.WhenAll(workers).ConfigureAwait(false);
            return results;
        }

        private async Task<DnsResponse> QueryFirstSuccessAsync(string name, DnsRecordType type, CancellationToken ct) {
            // Keep a rolling window of in-flight queries up to MaxParallelism and
            // immediately backfill slots after failures so queued endpoints are not blocked
            // by a slow failing endpoint from the first batch.
            int batch = Math.Max(1, _options.MaxParallelism);
            var queue = new Queue<DnsResolverEndpoint>(_endpoints);
            using var globalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            DnsResponse? bestError = null;

            var current = new List<Task<(DnsResponse resp, DnsResolverEndpoint ep)>>();
            for (int i = 0; i < batch && queue.Count > 0; i++) {
                var ep = queue.Dequeue();
                current.Add(InvokeEndpoint(ep, name, type, globalCts.Token));
            }

            while (current.Count > 0) {
                var finished = await Task.WhenAny(current).ConfigureAwait(false);
                current.Remove(finished);
                (DnsResponse resp, DnsResolverEndpoint ep) = await finished.ConfigureAwait(false);
                if (IsTerminal(resp)) {
                    globalCts.Cancel(); // cancel remaining tasks
                    _ = ObserveAsync(current);
                    return resp;
                }

                bestError = ChooseBetterError(bestError, resp);

                if (queue.Count > 0) {
                    var next = queue.Dequeue();
                    current.Add(InvokeEndpoint(next, name, type, globalCts.Token));
                }
            }

            return bestError ?? MakeError(name, type, DnsQueryErrorCode.ServFail, "All endpoints failed");
        }

        private async Task<DnsResponse> QuerySequentialAsync(string name, DnsRecordType type, CancellationToken ct) {
            DnsResponse? bestError = null;
            foreach (var ep in _endpoints) {
                ct.ThrowIfCancellationRequested();
                var (resp, _) = await InvokeEndpoint(ep, name, type, ct).ConfigureAwait(false);
                if (IsTerminal(resp)) return resp;
                bestError = ChooseBetterError(bestError, resp);
            }
            return bestError ?? MakeError(name, type, DnsQueryErrorCode.ServFail, "All endpoints failed");
        }

        private async Task<DnsResponse> QueryFastestWinsAsync(string name, DnsRecordType type, CancellationToken ct) {
            string key = _endpointSetKey;
            if (_options.EnableFastestCache && FastestCache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow) {
                // The cache stores endpoint key - resolve to endpoint
                var ep = _endpoints.FirstOrDefault(e => EndpointKey(e) == cached.Key);
                if (ep != null) {
                    var (resp, _) = await InvokeEndpoint(ep, name, type, ct).ConfigureAwait(false);
                    if (IsTerminal(resp)) return resp;
                    // fallback to warm all below if cached failed
                }
            }

            // Race endpoints in a bounded window. The first terminal DNS response is the fastest winner.
            int batch = Math.Max(1, _options.MaxParallelism);
            var queue = new Queue<DnsResolverEndpoint>(_endpoints);
            using var globalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var inflight = new List<Task<(DnsResponse resp, DnsResolverEndpoint ep, TimeSpan rtt)>>();

            // prime first batch
            for (int i = 0; i < batch && queue.Count > 0; i++) {
                var ep = queue.Dequeue();
                inflight.Add(InvokeEndpointTimed(ep, name, type, globalCts.Token));
            }

            DnsResponse? bestError = null;

            while (inflight.Count > 0) {
                var finished = await Task.WhenAny(inflight).ConfigureAwait(false);
                inflight.Remove(finished);
                var (resp, ep, rtt) = await finished.ConfigureAwait(false);
                if (IsTerminal(resp)) {
                    if (_options.EnableFastestCache) {
                        string endpointKey = EndpointKey(ep);
                        DateTime expires = DateTime.UtcNow.Add(_options.FastestCacheDuration);
                        StoreFastest(key, new FastestCacheEntry(endpointKey, expires));
                    }
                    globalCts.Cancel();
                    _ = ObserveAsync(inflight);
                    return resp;
                } else {
                    bestError = ChooseBetterError(bestError, resp);
                }

                // launch next if available
                if (queue.Count > 0) {
                    var next = queue.Dequeue();
                    inflight.Add(InvokeEndpointTimed(next, name, type, globalCts.Token));
                }
            }

            return bestError ?? MakeError(name, type, DnsQueryErrorCode.ServFail, "All endpoints failed");
        }

        private async Task<DnsResponse> QueryRoundRobinAsync(string name, DnsRecordType type, CancellationToken ct) {
            // Pick assigned endpoint by round-robin
            int idx = (int)((uint)Interlocked.Increment(ref _roundRobinIndex) % (uint)_endpoints.Length);
            var assigned = _endpoints[idx];
            var (resp, _) = await InvokeEndpoint(assigned, name, type, ct).ConfigureAwait(false);
            if (IsTerminal(resp)) return resp;

            // Fallback to first endpoint if different
            var first = _endpoints[0];
            if (!ReferenceEquals(first, assigned)) {
                var (fallback, _) = await InvokeEndpoint(first, name, type, ct).ConfigureAwait(false);
                if (IsTerminal(fallback)) return fallback;
                return ChooseBetterError(resp, fallback);
            }

            // If assigned was first, try second if available as a modest fallback
            if (_endpoints.Length > 1) {
                var second = _endpoints[1];
                var (fallback2, _) = await InvokeEndpoint(second, name, type, ct).ConfigureAwait(false);
                if (IsTerminal(fallback2)) return fallback2;
                return ChooseBetterError(resp, fallback2);
            }

            return resp;
        }

        private async Task<DnsResponse> QueryRandomAsync(string name, DnsRecordType type, CancellationToken ct) {
            int start = NextRandom(_endpoints.Length);
            DnsResponse? bestError = null;
            for (int offset = 0; offset < _endpoints.Length; offset++) {
                ct.ThrowIfCancellationRequested();
                DnsResolverEndpoint endpoint = _endpoints[(start + offset) % _endpoints.Length];
                var (response, _) = await InvokeEndpoint(endpoint, name, type, ct).ConfigureAwait(false);
                if (IsTerminal(response)) return response;
                bestError = ChooseBetterError(bestError, response);
            }
            return bestError ?? MakeError(name, type, DnsQueryErrorCode.ServFail, "All endpoints failed");
        }

        private static bool IsTerminal(DnsResponse resp) {
            return resp != null &&
                   (resp.Status == DnsResponseCode.NoError || resp.Status == DnsResponseCode.NXDomain) &&
                   string.IsNullOrEmpty(resp.Error);
        }

        private static async Task ObserveAsync<T>(IEnumerable<Task<T>> tasks) {
            try {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            } catch {
                // Losing requests are canceled intentionally; observing prevents unhandled task exceptions.
            }
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
                await _globalLimiter.WaitAsync(cts.Token).ConfigureAwait(false);
                try {
                SemaphoreSlim? limiter = GetLimiter(ep);
                if (limiter != null) await limiter.WaitAsync(cts.Token).ConfigureAwait(false);
                try {
                    var resolver = ResolveOverride;
                    if (resolver != null) {
                        response = await resolver(ep, name, type, cts.Token).ConfigureAwait(false);
                    } else {
                        response = await PerformQuery(ep, name, type, cts.Token).ConfigureAwait(false);
                    }
                } finally {
                    if (limiter != null) limiter.Release();
                }
                } finally {
                    _globalLimiter.Release();
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

        private static string ComputeSetKey(IEnumerable<DnsResolverEndpoint> endpoints) {
            var keys = endpoints.Select(EndpointKey).OrderBy(k => k, StringComparer.Ordinal);
            return string.Join("|", keys);
        }
        private static string EndpointKey(DnsResolverEndpoint ep) =>
            $"{ep.Transport}:{(ep.RequestFormat ?? MapTransport(ep.Transport))}:{(ep.DohUrl?.ToString() ?? (ep.Host ?? string.Empty))}:{ep.Port}:" +
            $"{ep.Family}:{ep.TlsServerName}:{ep.AllowTcpFallback}:{ep.EdnsBufferSize}:{ep.DnsSecOk}:{ep.Timeout?.Ticks}";

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
            Transport.Quic => DnsRequestFormat.DnsOverQuic,
            Transport.Grpc => DnsRequestFormat.DnsOverGrpc,
            Transport.Multicast => DnsRequestFormat.Multicast,
            _ => DnsRequestFormat.DnsOverUDP
        };

        private async Task<DnsResponse> PerformQuery(DnsResolverEndpoint ep, string name, DnsRecordType type, CancellationToken ct) {
            ClientX client = _clients.GetOrAdd(EndpointKey(ep), _ =>
                new Lazy<ClientX>(() => CreateClient(ep), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            bool requestDnsSec = _options.RequestDnsSec || _options.ValidateDnsSec || ep.DnsSecOk == true;
            return await client.Resolve(
                name,
                type,
                requestDnsSec: requestDnsSec,
                validateDnsSec: _options.ValidateDnsSec,
                returnAllTypes: false,
                retryOnTransient: false,
                typedRecords: _options.TypedRecords,
                parseTypedTxtRecords: _options.ParseTypedTxtRecords,
                cancellationToken: ct).ConfigureAwait(false);
        }

        private static void StoreFastest(string key, FastestCacheEntry entry) {
            lock (FastestCacheGate) {
                DateTime now = DateTime.UtcNow;
                foreach (KeyValuePair<string, FastestCacheEntry> item in FastestCache) {
                    if (item.Value.ExpiresAt <= now) FastestCache.TryRemove(item.Key, out _);
                }
                while (FastestCache.Count >= FastestCacheCapacity) {
                    KeyValuePair<string, FastestCacheEntry> oldest = FastestCache
                        .OrderBy(item => item.Value.ExpiresAt)
                        .FirstOrDefault();
                    if (oldest.Key == null || !FastestCache.TryRemove(oldest.Key, out _)) break;
                }
                FastestCache[key] = entry;
            }
        }

        private static int NextRandom(int maximum) {
            Func<int, int>? randomOverride = RandomIndexOverride;
            if (randomOverride != null) return (int)((uint)randomOverride(maximum) % (uint)maximum);
#if NET6_0_OR_GREATER
            return Random.Shared.Next(maximum);
#else
            lock (RandomLock) return RandomGenerator.Next(maximum);
#endif
        }

        internal static int FastestCacheCount => FastestCache.Count;

#if !NET6_0_OR_GREATER
        private static readonly object RandomLock = new();
        private static readonly Random RandomGenerator = new();
#endif

        private ClientX CreateClient(DnsResolverEndpoint ep) {
            ClientX client;
            DnsRequestFormat requestFormat = ep.RequestFormat ?? MapTransport(ep.Transport);
            TimeSpan effectiveTimeout = (_options.RespectEndpointTimeout && ep.Timeout.HasValue)
                ? ep.Timeout.Value
                : (_options.DefaultTimeout ?? TimeSpan.FromMilliseconds(DefaultPerQueryTimeoutMs));
            int effectiveTimeoutMilliseconds = effectiveTimeout <= TimeSpan.Zero
                ? int.MaxValue
                : (int)Math.Min(int.MaxValue, Math.Max(1, effectiveTimeout.TotalMilliseconds));
            if (ep.Transport == Transport.Doh) {
                var dohUri = ep.DohUrl ?? new Uri($"https://{ep.Host}/dns-query");
                client = new ClientX(
                    baseUri: dohUri,
                    requestFormat: requestFormat,
                    timeOutMilliseconds: effectiveTimeoutMilliseconds,
                    userAgent: _options.UserAgent,
                    httpVersion: _options.HttpVersion,
                    ignoreCertificateErrors: _options.IgnoreCertificateErrors,
                    enableCache: _options.EnableResponseCache,
                    useTcpFallback: _options.UseTcpFallback,
                    webProxy: _options.WebProxy,
                    maxConnectionsPerServer: _options.MaxConnectionsPerServer > 0 ? _options.MaxConnectionsPerServer : Configuration.DefaultMaxConnectionsPerServer);
            } else {
                if (string.IsNullOrWhiteSpace(ep.Host)) throw new ArgumentException("Endpoint.Host is required for non-DoH transports");
                client = new ClientX(
                    hostname: ep.Host!,
                    requestFormat: requestFormat,
                    timeOutMilliseconds: effectiveTimeoutMilliseconds,
                    userAgent: _options.UserAgent,
                    httpVersion: _options.HttpVersion,
                    ignoreCertificateErrors: _options.IgnoreCertificateErrors,
                    enableCache: _options.EnableResponseCache,
                    useTcpFallback: _options.UseTcpFallback,
                    webProxy: _options.WebProxy,
                    maxConnectionsPerServer: _options.MaxConnectionsPerServer > 0 ? _options.MaxConnectionsPerServer : Configuration.DefaultMaxConnectionsPerServer);
            }

            if (ep.Transport != Transport.Doh) {
                client.EndpointConfiguration.Port = ep.Port > 0 ? ep.Port : (ep.Transport == Transport.Dot ? 853 : 53);
            } else {
                client.EndpointConfiguration.Port = (ep.DohUrl?.IsDefaultPort ?? true) ? 443 : ep.DohUrl!.Port;
            }
            client.EndpointConfiguration.UseTcpFallback = _options.UseTcpFallback && ep.AllowTcpFallback;
            client.EndpointConfiguration.PreferredAddressFamily = ep.Family ??
                (_options.PreferIpv6 ? AddressFamily.InterNetworkV6 : (AddressFamily?)null);
            client.EndpointConfiguration.TlsServerName = ep.TlsServerName;
            client.EndpointConfiguration.MaxConcurrency = _options.MaxConcurrency;
            if (ep.EdnsBufferSize.HasValue) client.EndpointConfiguration.UdpBufferSize = ep.EdnsBufferSize.Value;
            client.EndpointConfiguration.CheckingDisabled = _options.CheckingDisabled;
            if (_options.EdnsOptions != null) client.EndpointConfiguration.EdnsOptions = _options.EdnsOptions;
            if (_options.EnableResponseCache && _options.MaxCacheTtl.HasValue) client.MaxCacheTtl = _options.MaxCacheTtl.Value;
            return client;
        }
        /// <summary>
        /// Disposes pooled endpoint clients and prevents new queries.
        /// Concurrent calls to <see cref="Dispose"/> and query methods are not supported.
        /// </summary>
        public void Dispose() {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _endpointLimiters.Clear();
            foreach (Lazy<ClientX> client in _clients.Values) {
                if (client.IsValueCreated) client.Value.Dispose();
            }
            _clients.Clear();
            // SemaphoreSlim allocates no native resource unless AvailableWaitHandle is used.
            // Leaving the limiters undisposed avoids racing Release() in an already-running query.
        }

        private void ThrowIfDisposed() {
            if (Volatile.Read(ref _disposed) != 0) {
                throw new ObjectDisposedException(nameof(DnsMultiResolver));
            }
        }
    }
}
