using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
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
            CancellationToken cancellationToken = default) {
            if (retryOnTransient) {
                try {
                    Action? beforeRetry = EndpointConfiguration.SelectionStrategy == DnsSelectionStrategy.Failover
                        ? EndpointConfiguration.AdvanceToNextHostname
                        : null;

                    return await RetryAsync(
                        () => ResolveInternal(name, type, requestDnsSec, validateDnsSec, returnAllTypes, cancellationToken),
                        maxRetries,
                        retryDelayMs,
                        beforeRetry);
                } catch (DnsClientException ex) {
                    return ex.Response;
                }
            } else {
                return await ResolveInternal(name, type, requestDnsSec, validateDnsSec, returnAllTypes, cancellationToken);
            }
        }

        private async Task<DnsResponse> ResolveInternal(string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool returnAllTypes, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            // lets we execute valid dns host name strategy
            EndpointConfiguration.SelectHostNameStrategy();

            // Get the HttpClient for the current strategy
            Client = GetClient(EndpointConfiguration.SelectionStrategy);

            string cacheKey = $"{EndpointConfiguration.BaseUri}|{type}|{name}";
            if (_cacheEnabled && _cache.TryGet(cacheKey, out var cached)) {
                return cached;
            }

            if (type == DnsRecordType.PTR) {
                // if we have PTR we need to convert it to proper format, just in case user didn't provide as with one
                name = ConvertToPtrFormat(name);
            }
            // Convert the domain name to punycode if it contains non-ASCII characters
            name = ConvertToPunycode(name);

            var auditEntry = EnableAudit ? new AuditEntry(name, type) : null;

            DnsResponse response;
            try {
                if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSON) {
                    response = await Client.ResolveJsonFormat(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken);
            } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttps) {
                response = await Client.ResolveWireFormatGet(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken);
            } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsPOST) {
                response = await Client.ResolveWireFormatPost(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken);
            } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttp3) {
                response = await Client.ResolveWireFormatHttp3(name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken);
            } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverTLS) {
                response = await DnsWireResolveDot.ResolveWireFormatDoT(EndpointConfiguration.Hostname, EndpointConfiguration.Port, name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, IgnoreCertificateErrors, cancellationToken);
            } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverQuic) {
#if NET8_0_OR_GREATER
                response = await DnsWireResolveQuic.ResolveWireFormatQuic(EndpointConfiguration.Hostname, EndpointConfiguration.Port, name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken);
#else
                throw new DnsClientException("DNS over QUIC is not supported on this platform.");
#endif
            } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverTCP) {
                response = await DnsWireResolveTcp.ResolveWireFormatTcp(EndpointConfiguration.Hostname, EndpointConfiguration.Port, name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken);
            } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverUDP) {
                response = await DnsWireResolveUdp.ResolveWireFormatUdp(EndpointConfiguration.Hostname, EndpointConfiguration.Port, name, type, requestDnsSec, validateDnsSec, Debug, EndpointConfiguration, cancellationToken);
            } else {
                throw new DnsClientException($"Invalid RequestFormat: {EndpointConfiguration.RequestFormat}");
            }
            } catch (Exception ex) {
                if (auditEntry != null) {
                    auditEntry.Exception = ex;
                    _auditTrail.Enqueue(auditEntry);
                }
                throw;
            }

            // Some DNS Providers return requested type, but also additional types
            // https://dns.quad9.net:5053/dns-query?name=autodiscover.evotec.pl&type=CNAME
            // We want to make sure the output is consistent
            if (!returnAllTypes && response.Answers != null) {
                response.Answers = response.Answers.Where(x => x.Type == type).ToArray();
            } else if (response.Answers == null) {
                response.Answers = Array.Empty<DnsAnswer>();
            }

            if (validateDnsSec) {
                bool hasRrsig = response.Answers != null && response.Answers.Any(a => a.Type == DnsRecordType.RRSIG);
                if (!response.AuthenticData && !hasRrsig) {
                    string validationError = "DNSSEC validation failed.";
                    response.Error = string.IsNullOrEmpty(response.Error) ? validationError : $"{response.Error} {validationError}";
                }
                if (EndpointConfiguration.ValidateRootDnsSec && (type == DnsRecordType.DS || type == DnsRecordType.DNSKEY)) {
                    if (!DnsSecValidator.ValidateAgainstRoot(response)) {
                        string validationError = "Root DNSSEC validation failed.";
                        response.Error = string.IsNullOrEmpty(response.Error) ? validationError : $"{response.Error} {validationError}";
                    }
                }
            }

            if (_cacheEnabled) {
                _cache.Set(cacheKey, response, CacheExpiration);
            }

            if (auditEntry != null) {
                auditEntry.Response = response;
                _auditTrail.Enqueue(auditEntry);
            }

            return response;
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
        /// <param name="useJitter">Whether to randomize delays with jitter for exponential backoff.</param>
        /// <remarks>
        /// The method retries when a transient exception occurs or when a <see cref="DnsResponse"/>
        /// returned by <paramref name="action"/> indicates a transient failure. Exponential backoff with
        /// jitter is used between attempts. If the final result still signals a transient error, a
        /// <see cref="DnsClientException"/> is thrown with the last response.
        /// </remarks>
        private static async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int delayMs = 100, Action? beforeRetry = null, bool useJitter = true) {
            Exception lastException = null;
            T lastResult = default(T);

            for (int attempt = 1; attempt <= maxRetries; attempt++) {
                try {
                    var result = await action();

                    // Special handling for DnsResponse - check if it indicates a transient error
                    if (result is DnsResponse response && IsTransientResponse(response)) {
                        lastResult = result;
                        if (attempt == maxRetries) {
                            // Break out of the loop so the transient result can be evaluated below
                            break;
                        }

                        beforeRetry?.Invoke();
                        int exponentialDelay = delayMs * (int)Math.Pow(2, attempt - 1);
                        int jitter = useJitter ? GetJitter(delayMs) : 0;
                        await Task.Delay(exponentialDelay + jitter);
                        continue;
                    }

                    // Success case or non-transient response
                    return result;
                } catch (Exception ex) when (IsTransient(ex)) {
                    lastException = ex;
                    if (attempt == maxRetries) {
                        // This was the last attempt, rethrow the exception
                        throw;
                    }

                    beforeRetry?.Invoke();
                    int exponentialDelay = delayMs * (int)Math.Pow(2, attempt - 1);
                    int jitter = useJitter ? GetJitter(delayMs) : 0;
                    await Task.Delay(exponentialDelay + jitter);
                    continue;
                }
            }

            // After retries, rethrow the last exception if there was one
            if (lastException != null) {
                throw lastException;
            }

            // If the last result indicates a transient failure, surface it as an exception
            if (lastResult is DnsResponse lastResponse && IsTransientResponse(lastResponse)) {
                throw new DnsClientException("Transient DNS response after maximum retries.", lastResponse);
            }

            return lastResult;
        }

        /// <summary>
        /// Checks if an error message indicates an SSL/TLS connection issue
        /// </summary>
        private static bool IsSSLConnectionError(string errorMessage) {
            if (string.IsNullOrEmpty(errorMessage)) return false;

            return errorMessage.IndexOf("ssl connection could not be established", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   errorMessage.IndexOf("unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   errorMessage.IndexOf("connection was forcibly closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   errorMessage.IndexOf("certificate validation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   errorMessage.IndexOf("handshake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   errorMessage.IndexOf("underlying connection was closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   errorMessage.IndexOf("unexpected error occurred on a send", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTransient(Exception ex) {
            // Handle DnsClientException with specific response codes
            if (ex is DnsClientException dnsEx) {
                DnsResponse? response = null;

                if (dnsEx.Response is not null) {
                    response = dnsEx.Response;
                } else if (dnsEx.Data.Contains("DnsResponse") && dnsEx.Data["DnsResponse"] is DnsResponse resp) {
                    response = resp;
                }

                if (response is not null) {
                    // Consider these DNS response codes as transient (should retry)
                    return response.Status == DnsResponseCode.ServerFailure ||
                           response.Status == DnsResponseCode.Refused ||
                           response.Status == DnsResponseCode.NotImplemented;
                }
            }

            // Check for SSL/TLS and connection-related errors (these often are transient)
            if (ex is HttpRequestException httpEx) {
                var message = httpEx.Message ?? string.Empty;
                var innerMessage = httpEx.InnerException?.Message ?? string.Empty;

                // SSL/TLS certificate and connection errors - these are often transient
                if (message.IndexOf("ssl connection could not be established", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("connection was forcibly closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("certificate validation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("handshake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    innerMessage.IndexOf("ssl connection could not be established", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    innerMessage.IndexOf("unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    innerMessage.IndexOf("connection was forcibly closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    innerMessage.IndexOf("certificate validation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    innerMessage.IndexOf("handshake", StringComparison.OrdinalIgnoreCase) >= 0) {
                    return true;
                }
            }

            if (ex is SocketException socketEx) {
                if (socketEx.SocketErrorCode == SocketError.ConnectionReset ||
                    socketEx.SocketErrorCode == SocketError.NetworkUnreachable) {
                    return true;
                }
            }

            // Network and timeout-related exceptions
            return ex is DnsClientException ||
                   ex is TaskCanceledException ||
                   ex is TimeoutException ||
                   ex is HttpRequestException ||
                   ex is SocketException ||
                   ex is System.IO.IOException ||
                   (ex.InnerException != null && IsTransient(ex.InnerException));
        }

        /// <summary>
        /// Checks if a DnsResponse indicates a transient error that should be retried
        /// </summary>
        private static bool IsTransientResponse(DnsResponse response) {
            // Only consider ServerFailure as transient if it has no answers and contains network/SSL errors
            if (response.Status == DnsResponseCode.ServerFailure) {
                // If there are answers despite ServerFailure, don't retry (it's likely a real DNS issue)
                if (response.Answers != null && response.Answers.Length > 0) {
                    return false;
                }

                // Check if the error indicates a network/SSL connection issue
                if (!string.IsNullOrEmpty(response.Error)) {
                    var errorMessage = response.Error;
                    if (errorMessage.IndexOf("ssl connection could not be established", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        errorMessage.IndexOf("unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        errorMessage.IndexOf("connection was forcibly closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        errorMessage.IndexOf("certificate validation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        errorMessage.IndexOf("handshake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        errorMessage.IndexOf("network error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        errorMessage.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        errorMessage.IndexOf("an error occurred while sending the request", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        errorMessage.IndexOf("unexpected error occurred on a send", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        errorMessage.IndexOf("underlying connection was closed", StringComparison.OrdinalIgnoreCase) >= 0) {
                        return true;
                    }
                }

                // ServerFailure with no answers and no clear error message might also be transient
                return true;
            }

            // Other potentially transient DNS response codes
            if (response.Status == DnsResponseCode.Refused ||
                response.Status == DnsResponseCode.NotImplemented) {
                return true;
            }

            return false;
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
        /// <returns>The DNS response.</returns>
        /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
        public DnsResponse ResolveSync(string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100) {
            return Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).RunSync();
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
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of DNS responses.</returns>
        /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
        public async Task<DnsResponse[]> Resolve(string name, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            Task<DnsResponse>[] tasks = new Task<DnsResponse>[types.Length];
            for (int i = 0; i < tasks.Length; i++) {
                tasks[i] = Resolve(name, types[i], requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken);
            }

            await Task.WhenAll(tasks);

            DnsResponse[] responses = new DnsResponse[tasks.Length];

            for (int i = 0; i < tasks.Length; i++) {
                responses[i] = await tasks[i];
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
        /// <returns>An array of DNS responses.</returns>
        /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
        public DnsResponse[] ResolveSync(string name, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200) {
            return Resolve(name, types, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).RunSync();
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
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of DNS responses.</returns>
        public async Task<DnsResponse[]> Resolve(string[] names, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            var tasks = new List<Task<DnsResponse>>();

            foreach (var name in names) {
                foreach (var type in types) {
                    tasks.Add(Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken));
                }
            }

            await Task.WhenAll(tasks);

            return tasks.Select(task => task.Result).ToArray();
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
        /// <returns>An array of DNS responses.</returns>
        public DnsResponse[] ResolveSync(string[] names, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200) {
            return Resolve(names, types, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).RunSync();
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
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The array of DNS responses from all queries.</returns>
        public async Task<DnsResponse[]> Resolve(string[] names, DnsRecordType type, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            var tasks = new List<Task<DnsResponse>>();

            foreach (var name in names) {
                tasks.Add(Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken));
            }

            await Task.WhenAll(tasks);

            return tasks.Select(task => task.Result).ToArray();
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
        /// <returns>An array of DNS responses.</returns>
        public DnsResponse[] ResolveSync(string[] names, DnsRecordType type, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200) {
            return Resolve(names, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).RunSync();
        }
    }
}
