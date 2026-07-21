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
            ThrowIfDisposed();
            using DnsClientTelemetry.DnsQueryTelemetryScope? telemetry = DnsClientTelemetry.StartQuery(name, type, EndpointConfiguration);
            try {
                _lastAuditEntryContext.Value = null;
                DnsResponse response;

                if (retryOnTransient) {
                    try {
                        Action<string>? onRetry = EnableAudit || EndpointConfiguration.SelectionStrategy == DnsSelectionStrategy.Failover
                            ? reason => HandleRetry(reason)
                            : null;

                        response = await RetryAsync(
                            () => ResolveWithSystemSearchDomains(name, type, requestDnsSec, validateDnsSec, returnAllTypes, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords, cancellationToken),
                            maxRetries,
                            retryDelayMs,
                            null,
                            onRetry,
                            true,
                            cancellationToken).ConfigureAwait(false);
                    } catch (DnsClientException ex) when (ex.Response != null) {
                        response = ex.Response;
                    }
                } else {
                    response = await ResolveWithSystemSearchDomains(name, type, requestDnsSec, validateDnsSec, returnAllTypes, maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords, cancellationToken).ConfigureAwait(false);
                }

                telemetry?.Complete(response);
                return response;
            } catch (Exception ex) {
                telemetry?.Fail(ex);
                throw;
            } finally {
                _lastAuditEntryContext.Value = null;
            }
        }

        private async Task<DnsResponse> ResolveInternal(string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool returnAllTypes, int maxRetries, int retryDelayMs, bool typedRecords, bool parseTypedTxtRecords, CancellationToken cancellationToken, bool dnsSecMaterialQuery = false, Configuration? queryConfigurationOverride = null, bool bypassSingleFlight = false, bool suppressAudit = false) {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");
            string inputName = name;
            if (EndpointConfiguration.LocalEndPoint != null && IsHttpBasedTransport(EndpointConfiguration.RequestFormat)) {
                throw new NotSupportedException(
                    $"LocalEndPoint is not supported by {EndpointConfiguration.RequestFormat}. Use UDP, TCP, DoT, or DoQ for explicit source binding.");
            }
            requestDnsSec = requestDnsSec || validateDnsSec;
            if (type == DnsRecordType.PTR) name = ConvertToPtrFormat(name);
            name = ConvertToPunycode(name);

            var auditEntry = !suppressAudit && EnableAudit ? new AuditEntry(name, type) { StartedAtUtc = DateTimeOffset.UtcNow } : null;
            var stopwatch = Stopwatch.StartNew();

            // Allow tests to override the resolver to avoid network calls.
            if (ResolverOverride != null) {
                try {
                    CaptureAuditConfiguration(auditEntry);
                    var overrideResponse = await ResolverOverride(name, type, cancellationToken).ConfigureAwait(false);
                    overrideResponse.ResponseSource = DnsResponseSource.Network;
                    FinalizeAuditEntry(auditEntry, overrideResponse, stopwatch);
                    return overrideResponse;
                } catch (Exception ex) {
                    FinalizeAuditException(auditEntry, ex, stopwatch);
                    throw;
                }
            }

            try {
                // lets we execute valid dns host name strategy
                Configuration queryConfiguration = queryConfigurationOverride ?? EndpointConfiguration.CreateQuerySnapshot(name);
                if (queryConfiguration.LocalEndPoint != null && IsHttpBasedTransport(queryConfiguration.RequestFormat)) {
                    throw new NotSupportedException(
                        $"LocalEndPoint is not supported by {queryConfiguration.RequestFormat}. Use UDP, TCP, DoT, or DoQ for explicit source binding.");
                }
                if (queryConfiguration.EnableTcpConnectionReuse && queryConfiguration.MaxTcpQueriesPerConnection <= 0) {
                    throw new ArgumentOutOfRangeException(nameof(queryConfiguration.MaxTcpQueriesPerConnection),
                        "MaxTcpQueriesPerConnection must be greater than zero when connection reuse is enabled.");
                }
                CaptureAuditConfiguration(auditEntry, queryConfiguration);

                string? cacheKey = null;
                if (!dnsSecMaterialQuery && _cacheEnabled) {
                    cacheKey = DnsCacheKeyBuilder.Build(queryConfiguration, name, type, requestDnsSec,
                        validateDnsSec, returnAllTypes, typedRecords, parseTypedTxtRecords, MaxCacheTtl,
                        IgnoreCertificateErrors);
                    if (_cache.TryGet(cacheKey, out var cached)) {
                        cached.ResponseSource = DnsResponseSource.Cache;
                        FinalizeAuditEntry(auditEntry, cached, stopwatch, servedFromCache: true);
                        return cached;
                    }
                }

                if (cacheKey != null && !bypassSingleFlight) {
                    var candidate = new Lazy<Task<DnsResponse>>(
                        () => ResolveInternal(inputName, type, requestDnsSec, validateDnsSec, returnAllTypes,
                            maxRetries, retryDelayMs, typedRecords, parseTypedTxtRecords,
                            CancellationToken.None, dnsSecMaterialQuery, queryConfiguration,
                            bypassSingleFlight: true, suppressAudit: true),
                        LazyThreadSafetyMode.ExecutionAndPublication);
                    Lazy<Task<DnsResponse>> flight = _cacheInflight.GetOrAdd(cacheKey, candidate);
                    bool ownsFlight = ReferenceEquals(candidate, flight);
                    Task<DnsResponse> flightTask = flight.Value;
                    _ = RemoveCacheFlightWhenCompletedAsync(cacheKey, flight, flightTask);
                    DnsResponse shared = await WaitForSharedResponseAsync(flightTask, cancellationToken)
                        .ConfigureAwait(false);
                    DnsResponse result = shared.Clone();
                    if (!ownsFlight) result.ResponseSource = DnsResponseSource.CoalescedNetwork;
                    FinalizeAuditEntry(auditEntry, result, stopwatch,
                        result.Status != DnsResponseCode.NoError || !string.IsNullOrEmpty(result.Error)
                            ? new DnsClientException(result.Error ?? "DNS query failed", result)
                            : null,
                        servedFromCache: result.ResponseSource == DnsResponseSource.Cache);
                    return result;
                }

                DnsResponse response;
                bool wireValidationQuery = validateDnsSec || dnsSecMaterialQuery;
                if (queryConfiguration.BuiltInEndpoint == DnsEndpoint.RootServer) {
                    if (queryConfiguration.IterativeMaxHops <= 0) {
                        throw new ArgumentOutOfRangeException(nameof(queryConfiguration.IterativeMaxHops));
                    }
                    response = await ResolveFromRootCore(
                        name,
                        type,
                        servers: null,
                        queryConfiguration.IterativeMaxHops,
                        queryConfiguration.Port,
                        requestDnsSec,
                        validateDnsSec,
                        queryConfiguration.EnableQNameMinimization,
                        queryConfiguration.Rfc5011TrustAnchorStorePath,
                        queryConfiguration.DnsSecSignatureVerifier,
                        cancellationToken).ConfigureAwait(false);
                } else {
                    // Get the HTTP client only for transports that can use it. Root iteration stays
                    // entirely on the shared wire engine and does not allocate an unused handler.
                    HttpClient queryClient = GetClient(queryConfiguration);
                    if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSON) {
                    response = wireValidationQuery
                        ? await queryClient.ResolveWireFormatGet(name, type, requestDnsSec, true, Debug, queryConfiguration, cancellationToken, useStandardDnsQueryPath: true).ConfigureAwait(false)
                        : await queryClient.ResolveJsonFormat(name, type, requestDnsSec, false, Debug, queryConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttps) {
                    response = await queryClient.ResolveWireFormatGet(name, type, requestDnsSec, wireValidationQuery, Debug, queryConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsPOST ||
                           queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsWirePost) {
                    response = await queryClient.ResolveWireFormatPost(name, type, requestDnsSec, wireValidationQuery, Debug, queryConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSONPOST) {
                    response = wireValidationQuery
                        ? await queryClient.ResolveWireFormatGet(name, type, requestDnsSec, true, Debug, queryConfiguration, cancellationToken, useStandardDnsQueryPath: true).ConfigureAwait(false)
                        : await queryClient.ResolveJsonFormatPost(name, type, requestDnsSec, false, Debug, queryConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.ObliviousDnsOverHttps) {
                    throw new NotSupportedException("Oblivious DNS-over-HTTPS requires HPKE encapsulation and is not implemented by the dependency-free core package.");
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttp2) {
                    response = await queryClient.ResolveWireFormatHttp2(name, type, requestDnsSec, wireValidationQuery, Debug, queryConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttp3) {
#if NET8_0_OR_GREATER
                    response = await queryClient.ResolveWireFormatHttp3(name, type, requestDnsSec, wireValidationQuery, Debug, queryConfiguration, cancellationToken).ConfigureAwait(false);
#else
                    throw new DnsClientException("DNS over HTTP/3 is not supported on this platform.");
#endif
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverGrpc) {
#if NET8_0_OR_GREATER
                    response = await DnsWireResolveGrpc.ResolveWireFormatGrpc(queryConfiguration.Hostname!, queryConfiguration.Port, name, type, requestDnsSec, wireValidationQuery, Debug, queryConfiguration, cancellationToken).ConfigureAwait(false);
#else
                    throw new DnsClientException("DNS over gRPC is not supported on this platform.");
#endif
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverTLS) {
                    response = await DnsWireResolveDot.ResolveWireFormatDoT(queryConfiguration.Hostname!, queryConfiguration.Port, name, type, requestDnsSec, wireValidationQuery, Debug, queryConfiguration, IgnoreCertificateErrors, cancellationToken,
                        queryConfiguration.EnableTcpConnectionReuse ? _streamConnectionPool : null).ConfigureAwait(false);
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverQuic) {
#if NET8_0_OR_GREATER
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) {
                        response = await DnsWireResolveQuic.ResolveWireFormatQuic(queryConfiguration.Hostname!, queryConfiguration.Port, name, type, requestDnsSec, wireValidationQuery, Debug, queryConfiguration, cancellationToken, _quicConnectionPool).ConfigureAwait(false);
                } else {
                    response = new DnsResponse {
                        Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverQuic, Type = type, OriginalName = name } ],
                        Status = DnsResponseCode.NotImplemented
                    };
                    response.AddServerDetails(queryConfiguration);
                    response.Error = "DNS over QUIC is not supported on this platform.";
                }
#else
                    throw new DnsClientException("DNS over QUIC is not supported on this platform.");
#endif
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverTCP) {
                    response = await DnsWireResolveTcp.ResolveWireFormatTcp(queryConfiguration.Hostname!, queryConfiguration.Port, name, type, requestDnsSec, wireValidationQuery, Debug, queryConfiguration, cancellationToken,
                        queryConfiguration.EnableTcpConnectionReuse ? _streamConnectionPool : null).ConfigureAwait(false);
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsOverUDP) {
                    response = await DnsWireResolveUdp.ResolveWireFormatUdp(queryConfiguration.Hostname!, queryConfiguration.Port, name, type, requestDnsSec, wireValidationQuery, Debug, queryConfiguration, 1, cancellationToken, _udpClientPool,
                        queryConfiguration.EnableTcpConnectionReuse ? _streamConnectionPool : null).ConfigureAwait(false);
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.Multicast) {
                    response = await DnsWireResolveMulticast.ResolveWireFormatMulticast(queryConfiguration.Hostname!, queryConfiguration.Port, name, type, requestDnsSec, wireValidationQuery, Debug, queryConfiguration, cancellationToken).ConfigureAwait(false);
                } else if (queryConfiguration.RequestFormat == DnsRequestFormat.DnsCrypt ||
                           queryConfiguration.RequestFormat == DnsRequestFormat.DnsCryptRelay) {
                    throw new NotSupportedException("DNSCrypt v2 is reserved for an optional protocol package and is not implemented by the core package.");
                } else {
                    throw new DnsClientException($"Invalid RequestFormat: {queryConfiguration.RequestFormat}");
                }
                }
                if (validateDnsSec && !dnsSecMaterialQuery
                    && queryConfiguration.BuiltInEndpoint != DnsEndpoint.RootServer) {
                    var validator = new DnsSecValidationEngine((materialName, materialType, token) =>
                        ResolveInternal(materialName, materialType, requestDnsSec: true, validateDnsSec: false,
                            returnAllTypes: true, maxRetries: 1, retryDelayMs: retryDelayMs,
                            typedRecords: false, parseTypedTxtRecords: false, cancellationToken: token,
                            dnsSecMaterialQuery: true),
                        trustAnchorStorePath: queryConfiguration.Rfc5011TrustAnchorStorePath,
                        signatureVerifier: queryConfiguration.DnsSecSignatureVerifier);
                    DnsSecValidationResult validation = await validator.ValidateAsync(response, name, type, cancellationToken).ConfigureAwait(false);
                    response.DnsSecValidationStatus = validation.Status;
                    response.DnsSecValidationMessage = validation.Message;
                    if (validation.Status == DnsSecValidationStatus.Bogus || validation.Status == DnsSecValidationStatus.Indeterminate) {
                        string validationError = $"DNSSEC {validation.Status.ToString().ToLowerInvariant()}: {validation.Message}";
                        response.Error = string.IsNullOrEmpty(response.Error) ? validationError : $"{response.Error} {validationError}";
                    }
                }

                // Cache lifetime is determined by the complete answer. In particular, an address
                // reached through a CNAME must not outlive the alias RRset merely because callers
                // requested an answer projection that hides the CNAME.
                TimeSpan cacheTtl = GetCacheTtl(response);

                // Validate the complete wire answer first, then apply the caller's projection.
                ApplyAnswerProjection(response, name, type, returnAllTypes);

                if (typedRecords) {
                    response.TypedAnswers = response.Answers
                        .Select(a => DnsRecordFactory.Create(a, parseTypedTxtRecords))
                        .Where(o => o != null)
                        .ToArray()!;
                }
                response.RefreshDerivedData();

                if (!dnsSecMaterialQuery && _cacheEnabled) {
                    TimeSpan ttl = cacheTtl;
                    if (ttl > MaxCacheTtl) ttl = MaxCacheTtl;

                    if (ttl > TimeSpan.Zero) {
                        _cache.Set(cacheKey!, response, ttl);
                    }
                }

                response.ResponseSource = DnsResponseSource.Network;

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
            }
        }

        private static async Task<DnsResponse> WaitForSharedResponseAsync(Task<DnsResponse> task,
            CancellationToken cancellationToken) {
            if (task.IsCompleted) return await task.ConfigureAwait(false);
            Task completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken))
                .ConfigureAwait(false);
            if (completed != task) throw new OperationCanceledException(cancellationToken);
            return await task.ConfigureAwait(false);
        }

        private static async Task RemoveCacheFlightWhenCompletedAsync(string key,
            Lazy<Task<DnsResponse>> flight, Task<DnsResponse> task) {
            try {
                await task.ConfigureAwait(false);
            } catch {
                // Every waiter observes the original failure. Removal only controls future attempts.
            } finally {
                if (_cacheInflight.TryGetValue(key, out Lazy<Task<DnsResponse>>? current) &&
                    ReferenceEquals(current, flight)) {
                    _cacheInflight.TryRemove(key, out _);
                }
            }
        }

        internal static void ApplyAnswerProjection(
            DnsResponse response,
            string queryName,
            DnsRecordType type,
            bool returnAllTypes) {
            response.RequestedAnswerPresent = HasRequestedAnswer(response, queryName, type);
            DnsAnswer[] allAnswers = response.Answers ?? Array.Empty<DnsAnswer>();
            if (returnAllTypes || type == DnsRecordType.ANY) {
                response.Answers = allAnswers;
                return;
            }

            int matchingCount = 0;
            for (int index = 0; index < allAnswers.Length; index++) {
                if (allAnswers[index].Type == type) matchingCount++;
            }
            if (matchingCount == allAnswers.Length) {
                response.Answers = allAnswers;
                return;
            }

            var matchingAnswers = new DnsAnswer[matchingCount];
            int targetIndex = 0;
            for (int index = 0; index < allAnswers.Length; index++) {
                if (allAnswers[index].Type == type) matchingAnswers[targetIndex++] = allAnswers[index];
            }
            response.Answers = matchingAnswers;
        }

        private static TimeSpan GetCacheTtl(DnsResponse response) {
            if (response.Answers is { Length: > 0 }) {
                int minimum = response.Answers.Min(answer => Math.Max(0, answer.TTL));
                return minimum == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(minimum);
            }

            if (response.Status != DnsResponseCode.NoError && response.Status != DnsResponseCode.NXDomain) {
                return TimeSpan.Zero;
            }

            DnsAnswer soa = (response.Authorities ?? Array.Empty<DnsAnswer>())
                .FirstOrDefault(answer => answer.Type == DnsRecordType.SOA);
            if (soa.Type != DnsRecordType.SOA) return TimeSpan.Zero;
            string[] parts = soa.DataRaw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7 || !uint.TryParse(parts[parts.Length - 1], out uint minimumTtl)) return TimeSpan.Zero;
            uint soaTtl = (uint)Math.Max(0, soa.TTL);
            uint seconds = Math.Min(soaTtl, minimumTtl);
            return seconds == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(seconds);
        }

        private void CaptureAuditConfiguration(AuditEntry? auditEntry, Configuration? configuration = null) {
            if (auditEntry == null) {
                return;
            }

            Configuration selectedConfiguration = configuration ?? EndpointConfiguration;
            auditEntry.SelectionStrategy = selectedConfiguration.SelectionStrategy;
            auditEntry.RequestFormat = selectedConfiguration.RequestFormat;
            auditEntry.ResolverHost = selectedConfiguration.BaseUri?.Host ?? selectedConfiguration.Hostname;
            auditEntry.ResolverPort = selectedConfiguration.BaseUri?.Port ?? selectedConfiguration.Port;
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
                return await Task.WhenAll(tasksUnbounded).ConfigureAwait(false);
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
                return await Task.WhenAll(tasksUnbounded).ConfigureAwait(false);
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
