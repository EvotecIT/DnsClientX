using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class containing main resolve methods.
    /// </summary>
    /// <remarks>
    /// These methods return strongly typed answers for various record types.
    /// </remarks>
    public partial class ClientX {
        /// <summary>
        /// Resolves a domain name using DNS over HTTPS. This method provides full control over the output.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
        public async Task<DnsResponse> Resolve(
            string name,
            DnsRecordType type = DnsRecordType.A,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            bool typedRecords = false,
            bool parseTypedTxtRecords = false,
            CancellationToken cancellationToken = default) {
            _lastAuditEntryContext.Value = null;
            if (retryOnTransient) {
                try {
                    Action<string>? onRetry = EnableAudit || EndpointConfiguration.SelectionStrategy == DnsSelectionStrategy.Failover
                        ? reason => HandleRetry(reason)
                        : null;

                    return await RetryAsync(
                        () => ResolveInternal(name, type, requestDnsSec, validateDnsSec, returnAllTypes, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords, cancellationToken),
                        maxRetries,
                        retryDelayMs,
                        null,
                        onRetry,
                        true,
                        cancellationToken).ConfigureAwait(false);
                } catch (DnsClientException ex) {
                    return ex.Response ?? new DnsResponse();
                } finally {
                    _lastAuditEntryContext.Value = null;
                }
            } else {
                try {
                    return await ResolveInternal(name, type, requestDnsSec, validateDnsSec, returnAllTypes, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords, cancellationToken).ConfigureAwait(false);
                } finally {
                    _lastAuditEntryContext.Value = null;
                }
            }
        }

        private async Task<DnsResponse> ResolveInternal(string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool returnAllTypes, int maxRetries, int retryDelayMs, bool typedRecords, bool parseTypedTxtRecords, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var auditEntry = EnableAudit ? new AuditEntry(name, type) { StartedAtUtc = DateTimeOffset.UtcNow } : null;
            var stopwatch = Stopwatch.StartNew();

            // Allow tests to override the resolver to avoid network calls.
            if (ResolverOverride != null) {
                try {
                    CaptureAuditConfiguration(auditEntry);
                    var overrideResponse = await ResolverOverride(name, type, cancellationToken).ConfigureAwait(false);
                    FinalizeAuditEntry(auditEntry, overrideResponse, stopwatch);
                    return overrideResponse;
                } catch (Exception ex) {
                    FinalizeAuditException(auditEntry, ex, stopwatch);
                    throw;
                }
            }

            bool originalCd = EndpointConfiguration.CheckingDisabled;
            if (validateDnsSec) {
                EndpointConfiguration.CheckingDisabled = true;
            }

            try {
                // lets we execute valid dns host name strategy
                EndpointConfiguration.SelectHostNameStrategy();
                CaptureAuditConfiguration(auditEntry);

                // Get the HttpClient for the current strategy
                Client = GetClient(EndpointConfiguration.SelectionStrategy);

                string cacheKey = $"{EndpointConfiguration.BaseUri}|{type}|{name}";
                if (_cacheEnabled && _cache.TryGet(cacheKey, out var cached)) {
                    FinalizeAuditEntry(auditEntry, cached, stopwatch, servedFromCache: true);
                    return cached;
                }

                if (type == DnsRecordType.PTR) {
                    // if we have PTR we need to convert it to proper format, just in case user didn't provide as with one
                    name = ConvertToPtrFormat(name);
                }
                // Convert the domain name to punycode if it contains non-ASCII characters
                name = ConvertToPunycode(name);

                DnsResponse response;
                if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSON) {
                    response = await Client.ResolveJsonFormat(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttps) {
                    response = await Client.ResolveWireFormatGet(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsPOST ||
                           EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsWirePost) {
                    response = await Client.ResolveWireFormatPost(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSONPOST) {
                    response = await Client.ResolveJsonFormatPost(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.ObliviousDnsOverHttps) {
                    response = await Client.ResolveWireFormatGet(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttp2) {
                    response = await Client.ResolveWireFormatHttp2(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttp3) {
#if NET8_0_OR_GREATER
                    response = await Client.ResolveWireFormatHttp3(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
#else
                    throw new DnsClientException("DNS over HTTP/3 is not supported on this platform.");
#endif
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverGrpc) {
#if NET8_0_OR_GREATER
                    response = await DnsWireResolveGrpc.ResolveWireFormatGrpc(EndpointConfiguration.Hostname!, EndpointConfiguration.Port, name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
#else
                    throw new DnsClientException("DNS over gRPC is not supported on this platform.");
#endif
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverTLS) {
                    response = await DnsWireResolveDot.ResolveWireFormatDoT(EndpointConfiguration.Hostname!, EndpointConfiguration.Port, name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, IgnoreCertificateErrors, cancellationToken).ConfigureAwait(false);
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverQuic) {
#if NET8_0_OR_GREATER
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) {
                        response = await DnsWireResolveQuic.ResolveWireFormatQuic(EndpointConfiguration.Hostname!, EndpointConfiguration.Port, name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
                } else {
                    response = new DnsResponse {
                        Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverQuic, Type = type, OriginalName = name } ],
                        Status = DnsResponseCode.NotImplemented
                    };
                    response.AddServerDetails(EndpointConfiguration);
                    response.Error = "DNS over QUIC is not supported on this platform.";
                }
#else
                    throw new DnsClientException("DNS over QUIC is not supported on this platform.");
#endif
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverTCP) {
                    response = await DnsWireResolveTcp.ResolveWireFormatTcp(EndpointConfiguration.Hostname!, EndpointConfiguration.Port, name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverUDP) {
                    response = await DnsWireResolveUdp.ResolveWireFormatUdp(EndpointConfiguration.Hostname!, EndpointConfiguration.Port, name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, maxRetries, cancellationToken).ConfigureAwait(false);
                } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.Multicast) {
                    response = await DnsWireResolveMulticast.ResolveWireFormatMulticast(EndpointConfiguration.Hostname!, EndpointConfiguration.Port, name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
                } else {
                    throw new DnsClientException($"Invalid RequestFormat: {EndpointConfiguration.RequestFormat}");
                }
                // Some DNS Providers return requested type, but also additional types
                // https://dns.quad9.net:5053/dns-query?name=autodiscover.evotec.pl&type=CNAME
                // We want to make sure the output is consistent
                if (!returnAllTypes && response.Answers != null) {
                    response.Answers = response.Answers.Where(x => x.Type == type).ToArray();
                } else if (response.Answers == null) {
                    response.Answers = Array.Empty<DnsAnswer>();
                }

                if (typedRecords && response.Answers != null) {
                    response.TypedAnswers = response.Answers
                        .Select(a => DnsRecordFactory.Create(a, parseTypedTxtRecords))
                        .Where(o => o != null)
                        .ToArray()!;
                }

                if (validateDnsSec) {
                    bool hasRrsig = response.Answers != null && response.Answers.Any(a => a.Type == DnsRecordType.RRSIG);
                    if (!response.AuthenticData && !hasRrsig) {
                        string validationError = "DNSSEC validation failed.";
                        response.Error = string.IsNullOrEmpty(response.Error) ? validationError : $"{response.Error} {validationError}";
                    }
                    if (EndpointConfiguration.ValidateRootDnsSec && (type == DnsRecordType.DS || type == DnsRecordType.DNSKEY)) {
                        if (!DnsSecValidator.ValidateAgainstRoot(response, out string rootMessage)) {
                            string validationError = $"Root DNSSEC validation failed: {rootMessage}";
                            response.Error = string.IsNullOrEmpty(response.Error) ? validationError : $"{response.Error} {validationError}";
                        }
                    }
                    if (hasRrsig && !DnsSecValidator.ValidateChain(response, out string chainMessage)) {
                        string validationError = $"DNSSEC signature verification failed: {chainMessage}";
                        response.Error = string.IsNullOrEmpty(response.Error) ? validationError : $"{response.Error} {validationError}";
                    }
                }

                if (_cacheEnabled) {
                    TimeSpan ttl = CacheExpiration;
                    if (response.Answers != null && response.Answers.Length > 0) {
                        int minTtl = response.Answers.Min(a => a.TTL);
                        ttl = minTtl == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(minTtl);
                    }

                    if (ttl != TimeSpan.Zero && ttl < MinCacheTtl) ttl = MinCacheTtl;
                    if (ttl > MaxCacheTtl) ttl = MaxCacheTtl;

                    if (ttl > TimeSpan.Zero) {
                        _cache.Set(cacheKey, response, ttl);
                    }
                }

                FinalizeAuditEntry(
                    auditEntry,
                    response,
                    stopwatch,
                    response.Status != DnsResponseCode.NoError || !string.IsNullOrEmpty(response.Error)
                        ? new DnsClientException(response.Error ?? "DNS query failed", response)
                        : null);

                if (response.RoundTripTime <= TimeSpan.Zero) {
                    response.RoundTripTime = stopwatch.Elapsed;
                }

                return response;
            } catch (Exception ex) {
                FinalizeAuditException(auditEntry, ex, stopwatch);
                throw;
            } finally {
                EndpointConfiguration.CheckingDisabled = originalCd;
            }
        }

        private void CaptureAuditConfiguration(AuditEntry? auditEntry) {
            if (auditEntry == null) {
                return;
            }

            auditEntry.SelectionStrategy = EndpointConfiguration.SelectionStrategy;
            auditEntry.RequestFormat = EndpointConfiguration.RequestFormat;
            auditEntry.ResolverHost = EndpointConfiguration.BaseUri?.Host ?? EndpointConfiguration.Hostname;
            auditEntry.ResolverPort = EndpointConfiguration.BaseUri?.Port ?? EndpointConfiguration.Port;
        }

        private void FinalizeAuditEntry(AuditEntry? auditEntry, DnsResponse response, Stopwatch stopwatch, Exception? exception = null, bool servedFromCache = false) {
            if (auditEntry == null) {
                return;
            }

            if (exception == null && (response.Status != DnsResponseCode.NoError || !string.IsNullOrEmpty(response.Error))) {
                exception = new DnsClientException(response.Error ?? "DNS query failed", response);
            }

            auditEntry.Response = response;
            auditEntry.Exception = exception;
            auditEntry.Duration = stopwatch.Elapsed;
            auditEntry.ServedFromCache = servedFromCache;
            auditEntry.AttemptNumber = Interlocked.Increment(ref _auditAttemptCounter);
            auditEntry.UsedTransport = response.UsedTransport;
            _lastAuditEntryContext.Value = auditEntry;
            _auditTrail.Enqueue(auditEntry);
        }

        private void FinalizeAuditException(AuditEntry? auditEntry, Exception ex, Stopwatch stopwatch) {
            if (auditEntry == null) {
                return;
            }

            auditEntry.Duration = stopwatch.Elapsed;
            auditEntry.Exception = ex;
            auditEntry.AttemptNumber = Interlocked.Increment(ref _auditAttemptCounter);
            if (ex is DnsClientException dnsEx && dnsEx.Response != null) {
                auditEntry.Response = dnsEx.Response;
                auditEntry.UsedTransport = dnsEx.Response.UsedTransport;
            }
            _lastAuditEntryContext.Value = auditEntry;
            _auditTrail.Enqueue(auditEntry);
        }

        private void HandleRetry(string reason) {
            AuditEntry? auditEntry = GetCurrentAuditEntry();
            if (auditEntry != null) {
                AppendRetryReason(auditEntry, reason);
            }

            if (EndpointConfiguration.SelectionStrategy == DnsSelectionStrategy.Failover) {
                string? previousHost = EndpointConfiguration.Hostname;
                EndpointConfiguration.AdvanceToNextHostname();
                EndpointConfiguration.SelectHostNameStrategy();
                if (auditEntry != null) {
                    AppendRetryReason(auditEntry, $"failover {previousHost ?? "(unknown)"} -> {EndpointConfiguration.Hostname ?? "(unknown)"}");
                }
            }
        }

        private AuditEntry? GetCurrentAuditEntry() {
            if (_lastAuditEntryContext.Value != null) {
                return _lastAuditEntryContext.Value;
            }

            AuditEntry? last = null;
            foreach (AuditEntry entry in _auditTrail) {
                last = entry;
            }

            return last;
        }

        private static void AppendRetryReason(AuditEntry auditEntry, string detail) {
            if (string.IsNullOrWhiteSpace(detail)) {
                return;
            }

            auditEntry.RetryReason = string.IsNullOrWhiteSpace(auditEntry.RetryReason)
                ? detail
                : $"{auditEntry.RetryReason}; {detail}";
        }

        // Generate jitter in a thread-safe manner across all supported
        // frameworks. Use Random.Shared when available and fall back to
        // a locked Random instance on older targets.
#if NET6_0_OR_GREATER
        private static int GetJitter(int max) => Random.Shared.Next(max);
#else
        private static readonly object _randLock = new();
        private static readonly Random _rand = new();
        private static int GetJitter(int max) {
            if (max <= 0) return 0;
            lock (_randLock) {
                return _rand.Next(max);
            }
        }
#endif

        /// <summary>
        /// Executes the provided asynchronous <paramref name="action"/> with retry logic.
        /// </summary>
        /// <typeparam name="T">Type returned by the action.</typeparam>
        /// <param name="action">The asynchronous operation to execute.</param>
        /// <param name="maxRetries">Maximum number of attempts before giving up.</param>
        /// <param name="delayMs">Base delay between retries in milliseconds. The actual wait time grows exponentially with a random jitter.</param>
        /// <param name="beforeRetry">Optional callback invoked before each retry attempt.</param>
        /// <param name="onRetry">Optional callback invoked with a short reason describing why the retry was triggered.</param>
        /// <param name="useJitter">Whether to randomize delays with jitter for exponential backoff.</param>
        /// <param name="cancellationToken">Token used to cancel waits between retries.</param>
        /// <remarks>
        /// The method retries when a transient exception occurs or when a <see cref="DnsResponse"/>
        /// returned by <paramref name="action"/> indicates a transient failure. Exponential backoff with
        /// jitter is used between attempts. If the final result still signals a transient error, a
        /// <see cref="DnsClientException"/> is thrown with the last response.
        /// </remarks>
        private static async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int delayMs = 100, Action? beforeRetry = null, Action<string>? onRetry = null, bool useJitter = true, CancellationToken cancellationToken = default) {
            if (maxRetries == 0)
            {
                var result = await action().ConfigureAwait(false);
                if (result is DnsResponse dns)
                {
                    dns.RetryCount = 0;
                }
                return result;
            }

            Exception? lastException = null;
            DnsClientException? lastDnsClientException = null;
            T? lastResult = default;

            for (int attempt = 1; attempt <= maxRetries; attempt++) {
                try {
                    var result = await action().ConfigureAwait(false);

                    // Special handling for DnsResponse - check if it indicates a transient error
                    if (result is DnsResponse response && IsTransientResponse(response)) {
                        lastResult = result;
                        if (attempt == maxRetries) {
                            // Break out of the loop so the transient result can be evaluated below
                            break;
                        }

                        onRetry?.Invoke(DescribeRetryReason(response));
                        beforeRetry?.Invoke();
                        int exponentialDelay = delayMs <= 0
                            ? 0
                            : (int)Math.Min((long)delayMs << attempt, int.MaxValue);
                        int jitter = useJitter ? GetJitter(delayMs) : 0;
                        await Task.Delay(exponentialDelay + jitter, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // Success case or non-transient response
                    if (result is DnsResponse success)
                    {
                        success.RetryCount = attempt - 1;
                    }
                    return result;
                } catch (Exception ex) when (IsTransient(ex)) {
                    lastException = ex;
                    if (ex is DnsClientException dnsEx) {
                        lastDnsClientException = dnsEx;
                    }
                    if (attempt == maxRetries) {
                        // Break out of the loop so the last exception can be
                        // thrown after retries
                        break;
                    }

                    onRetry?.Invoke($"transient exception: {ex.GetType().Name}");
                    beforeRetry?.Invoke();
                    int exponentialDelay = delayMs <= 0
                        ? 0
                        : (int)Math.Min((long)delayMs << attempt, int.MaxValue);
                    int jitter = useJitter ? GetJitter(delayMs) : 0;
                    await Task.Delay(exponentialDelay + jitter, cancellationToken).ConfigureAwait(false);
                    continue;
                }
            }

            // After retries, rethrow the last DNS exception if captured
            if (lastDnsClientException != null) {
                throw lastDnsClientException;
            }

            // Rethrow any non-DNS transient exception captured
            if (lastException != null) {
                throw lastException;
            }

            // If the last result indicates a transient failure, surface it as an exception
            if (lastResult is DnsResponse lastResponse && IsTransientResponse(lastResponse))
            {
                lastResponse.RetryCount = maxRetries - 1;
                throw new DnsClientException("Transient DNS response after maximum retries.", lastResponse);
            }

            if (lastResult is DnsResponse finalResponse)
            {
                finalResponse.RetryCount = maxRetries - 1;
            }
            return lastResult!;
        }

        private static bool IsTransient(Exception ex) {
            return DnsQueryDiagnostics.IsTransient(ex);
        }

        /// <summary>
        /// Checks if a DnsResponse indicates a transient error that should be retried
        /// </summary>
        private static bool IsTransientResponse(DnsResponse response) {
            return DnsQueryDiagnostics.IsTransient(response);
        }

        private static string DescribeRetryReason(DnsResponse response) {
            if (response.IsTruncated) {
                return "transient response: truncated";
            }

            if (response.ErrorCode != DnsQueryErrorCode.None) {
                return $"transient response: {response.ErrorCode}";
            }

            return $"transient response: {response.Status}";
        }

        /// <summary>
        /// Resolves a domain name using DNS over HTTPS. This method provides full control over the output. Synchronous version.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <returns>The DNS response.</returns>
        /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
        public DnsResponse ResolveSync(string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100, bool typedRecords = false, bool parseTypedTxtRecords = false) {
            return Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords).RunSync();
        }

        /// <summary>
        /// Resolves multiple DNS resource types for a domain name in parallel using DNS over HTTPS.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="types">The array of DNS resource record types to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of DNS responses.</returns>
        /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
        public async Task<DnsResponse[]> Resolve(string name, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, bool typedRecords = false, bool parseTypedTxtRecords = false, CancellationToken cancellationToken = default) {
            Task<DnsResponse>[] tasks = new Task<DnsResponse>[types.Length];
            for (int i = 0; i < tasks.Length; i++) {
                tasks[i] = Resolve(name, types[i], requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords, cancellationToken);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            DnsResponse[] responses = new DnsResponse[tasks.Length];

            for (int i = 0; i < tasks.Length; i++) {
                responses[i] = await tasks[i].ConfigureAwait(false);
            }

            return responses;
        }

        /// <summary>
        /// Resolves multiple DNS resource types for a domain name in parallel using DNS over HTTPS. Synchronous version.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="types">The array of DNS resource record types to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <returns>An array of DNS responses.</returns>
        /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
        public DnsResponse[] ResolveSync(string name, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, bool typedRecords = false, bool parseTypedTxtRecords = false) {
            return Resolve(name, types, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords).RunSync();
        }

        /// <summary>
        /// Resolves multiple domain names for multiple DNS record types in parallel using DNS over HTTPS.
        /// </summary>
        /// <param name="names">The array of domain names to resolve.</param>
        /// <param name="types">The array of DNS resource record types to resolve.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of DNS responses.</returns>
        public async Task<DnsResponse[]> Resolve(string[] names, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, bool typedRecords = false, bool parseTypedTxtRecords = false, CancellationToken cancellationToken = default) {
            int total = names.Length * types.Length;
            if (EndpointConfiguration.MaxConcurrency is null || EndpointConfiguration.MaxConcurrency <= 0 || EndpointConfiguration.MaxConcurrency >= total) {
                var tasksUnbounded = new List<Task<DnsResponse>>();
                foreach (var name in names) {
                    foreach (var type in types) {
                        tasksUnbounded.Add(Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords, cancellationToken));
                    }
                }
                await Task.WhenAll(tasksUnbounded).ConfigureAwait(false);
                return tasksUnbounded.Select(task => task.Result).ToArray();
            }

            var responses = new DnsResponse[total];
            using var semaphore = new System.Threading.SemaphoreSlim(EndpointConfiguration.MaxConcurrency.Value);
            var tasks = new List<Task>(total);

            async Task RunOneAsync(int idx, string queryName, DnsRecordType queryType) {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try {
                    responses[idx] = await Resolve(queryName, queryType, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords, cancellationToken).ConfigureAwait(false);
                } finally {
                    semaphore.Release();
                }
            }

            int index = 0;
            for (int i = 0; i < names.Length; i++) {
                for (int j = 0; j < types.Length; j++) {
                    var idx = index++;
                    var name = names[i];
                    var type = types[j];
                    tasks.Add(RunOneAsync(idx, name, type));
                }
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return responses;
        }

        /// <summary>
        /// Resolves multiple domain names for multiple DNS record types in parallel using DNS over HTTPS. Synchronous version.
        /// </summary>
        /// <param name="names">The array of domain names to resolve.</param>
        /// <param name="types">The array of DNS resource record types to resolve.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <returns>An array of DNS responses.</returns>
        public DnsResponse[] ResolveSync(string[] names, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, bool typedRecords = false, bool parseTypedTxtRecords = false) {
            return Resolve(names, types, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords).RunSync();
        }

        /// <summary>
        /// Resolves multiple domain names for single DNS record type in parallel using DNS over HTTPS.
        /// </summary>
        /// <param name="names">The names.</param>
        /// <param name="type">The type.</param>
        /// <param name="requestDnsSec">if set to <c>true</c> [request DNS sec].</param>
        /// <param name="validateDnsSec">if set to <c>true</c> [validate DNS sec].</param>
        /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The array of DNS responses from all queries.</returns>
        public async Task<DnsResponse[]> Resolve(string[] names, DnsRecordType type, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, bool typedRecords = false, bool parseTypedTxtRecords = false, CancellationToken cancellationToken = default) {
            int total = names.Length;
            if (EndpointConfiguration.MaxConcurrency is null || EndpointConfiguration.MaxConcurrency <= 0 || EndpointConfiguration.MaxConcurrency >= total) {
                var tasksUnbounded = new List<Task<DnsResponse>>();
                foreach (var name in names) {
                    tasksUnbounded.Add(Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords, cancellationToken));
                }
                await Task.WhenAll(tasksUnbounded).ConfigureAwait(false);
                return tasksUnbounded.Select(task => task.Result).ToArray();
            }

            var responses = new DnsResponse[total];
            using var semaphore = new System.Threading.SemaphoreSlim(EndpointConfiguration.MaxConcurrency.Value);
            var tasks = new List<Task>(total);

            async Task RunOneAsync(int idx, string queryName) {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try {
                    responses[idx] = await Resolve(queryName, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords, cancellationToken).ConfigureAwait(false);
                } finally {
                    semaphore.Release();
                }
            }

            for (int i = 0; i < names.Length; i++) {
                var idx = i;
                var name = names[idx];
                tasks.Add(RunOneAsync(idx, name));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return responses;
        }

        /// <summary>
        /// Resolves multiple domain names for single DNS record type in parallel using DNS over HTTPS. Synchronous version.
        /// </summary>
        /// <param name="names">The names.</param>
        /// <param name="type">The type.</param>
        /// <param name="requestDnsSec">if set to <c>true</c> [request DNS sec].</param>
        /// <param name="validateDnsSec">if set to <c>true</c> [validate DNS sec].</param>
        /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <returns>An array of DNS responses.</returns>
        public DnsResponse[] ResolveSync(string[] names, DnsRecordType type, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, bool typedRecords = false, bool parseTypedTxtRecords = false) {
            return Resolve(names, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords).RunSync();
        }

        /// <summary>
        /// Resolves a domain name pattern by expanding wildcards into multiple queries.
        /// Supported patterns include numeric ranges like <c>server[1-3].example.com</c>
        /// and brace expansions such as <c>host{a,b}.example.com</c>.
        /// </summary>
        /// <param name="pattern">The pattern containing wildcards.</param>
        /// <param name="type">The DNS resource type to resolve.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all record types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">Maximum number of retries.</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>Array of DNS responses for each expanded query.</returns>
        public async Task<DnsResponse[]> ResolvePattern(
            string pattern,
            DnsRecordType type = DnsRecordType.A,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            bool typedRecords = false,
            bool parseTypedTxtRecords = false,
            CancellationToken cancellationToken = default) {
            string[] names = ExpandPattern(pattern).ToArray();
            return await Resolve(names, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Expands a pattern containing brace or range expressions into a set of DNS names.
        /// </summary>
        /// <param name="pattern">The pattern which may contain brace expressions or numeric ranges.</param>
        /// <returns>An enumeration of expanded names.</returns>
        public static IEnumerable<string> ExpandPattern(string pattern) {
            if (string.IsNullOrEmpty(pattern)) yield break;

            var braceIndex = pattern.IndexOf('{');
            if (braceIndex >= 0) {
                int closing = FindClosingBrace(pattern, braceIndex);
                if (closing > braceIndex) {
                    string before = pattern.Substring(0, braceIndex);
                    string after = pattern.Substring(closing + 1);
                    string inner = pattern.Substring(braceIndex + 1, closing - braceIndex - 1);

                    IEnumerable<string> replacements;
                    var range = Regex.Match(inner, "^(\\d+)\\.\\.(\\d+)$");
                    if (range.Success) {
                        int start = int.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture);
                        int end = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
                        int width = range.Groups[1].Value.Length;
                        replacements = Enumerable.Range(start, end - start + 1)
                            .Select(i => i.ToString($"D{width}"));
                    } else {
                        replacements = inner.Split(',');
                    }

                    foreach (var rep in replacements) {
                        foreach (var s in ExpandPattern(before + rep + after))
                            yield return s;
                    }
                    yield break;
                }
            }

            var square = Regex.Match(pattern, "\\[(\\d+)-(\\d+)\\]");
            if (square.Success) {
                string before = pattern.Substring(0, square.Index);
                string after = pattern.Substring(square.Index + square.Length);
                int start = int.Parse(square.Groups[1].Value, CultureInfo.InvariantCulture);
                int end = int.Parse(square.Groups[2].Value, CultureInfo.InvariantCulture);
                int width = square.Groups[1].Value.Length;
                for (int i = start; i <= end; i++) {
                    foreach (var s in ExpandPattern(before + i.ToString($"D{width}") + after))
                        yield return s;
                }
                yield break;
            }

            int starIndex = pattern.IndexOf('*');
            if (starIndex >= 0) {
                string before = pattern.Substring(0, starIndex);
                string after = pattern.Substring(starIndex + 1);
                for (int i = 0; i <= 9; i++) {
                    foreach (var s in ExpandPattern(before + i + after))
                        yield return s;
                }
                yield break;
            }

            yield return pattern;
        }

        private static int FindClosingBrace(string pattern, int openIndex) {
            int depth = pattern[openIndex] == '{' ? 1 : 0;
            for (int i = openIndex + 1; i < pattern.Length; i++) {
                char c = pattern[i];
                if (c == '{') {
                    depth++;
                } else if (c == '}') {
                    depth--;
                    if (depth == 0) {
                        return i;
                    }
                }
            }

            return -1;
        }
    }}
