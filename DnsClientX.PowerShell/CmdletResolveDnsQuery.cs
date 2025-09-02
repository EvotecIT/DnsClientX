using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Net;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Resolves DNS records (A, AAAA, MX, TXT, …) over UDP/TCP/DoT/DoH with optional multi-resolver strategies.</para>
    /// <para type="description">Supports single-provider queries, multiple providers with FirstSuccess/FastestWins/SequentialAll/RoundRobin, direct endpoints (IPv4/IPv6/DoH URLs), concurrency control, and TTL-based response caching.</para>
    /// <example>
    ///  <para>Simple (system default)</para>
    ///  <code>Resolve-Dns -Name "example.com" -Type A</code>
    /// </example>
    /// <example>
    ///  <para>Single provider (classic)</para>
    ///  <code>Resolve-Dns -Name "example.com" -Type A -DnsProvider Cloudflare</code>
    /// </example>
    /// <example>
    ///  <para>FirstSuccess across providers</para>
    ///  <code>Resolve-Dns -Name "example.com" -Type A -DnsProvider Cloudflare,Google -ResolverStrategy FirstSuccess</code>
    /// </example>
    /// <example>
    ///  <para>FastestWins with cache</para>
    ///  <code>Resolve-Dns -Name "example.com" -Type A -DnsProvider Cloudflare,Google -ResolverStrategy FastestWins -FastestCacheMinutes 10 -ResponseCache</code>
    /// </example>
    /// <example>
    ///  <para>RoundRobin with per-endpoint cap</para>
    ///  <code>Resolve-Dns -Name @('a.com','b.com') -Type A -DnsProvider System,Cloudflare,Quad9 -ResolverStrategy RoundRobin -MaxParallelism 16 -PerEndpointMaxInFlight 4</code>
    /// </example>
    /// <example>
    ///  <para>Mixed endpoints (UDP + DoH)</para>
    ///  <code>Resolve-Dns -Name 'example.com' -Type TXT -ResolverEndpoint '1.1.1.1:53','https://dns.google/dns-query' -ResolverStrategy FirstSuccess</code>
    /// </example>
    /// <example>
    ///  <para>Enable TTL-based response cache with bounds</para>
    ///  <code>Resolve-Dns -Name 'example.com' -Type MX -DnsProvider Cloudflare,Google -ResponseCache -CacheExpirationSeconds 30 -MinCacheTtlSeconds 1 -MaxCacheTtlSeconds 3600</code>
    /// </example>
    /// </summary>
    /// <seealso cref="DnsClientX.PowerShell.AsyncPSCmdlet" />
    [Alias("Resolve-DnsQuery")]
    [Cmdlet(VerbsDiagnostic.Resolve, "Dns", DefaultParameterSetName = "ServerName")]
    public sealed class CmdletResolveDnsQuery : AsyncPSCmdlet {
        /// <summary>
        /// <para type="description">The name of the DNS record to query for</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ServerName")]
        public string[] Name { get; set; } = Array.Empty<string>();

        /// <summary>
        /// <para type="description">Pattern to expand into multiple DNS queries.</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternResolverDnsProvider")]
        public string? Pattern { get; set; }
        /// <summary>
        /// <para type="description">The type of the record to query for. If not specified, A record is queried.</para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "PatternResolverDnsProvider")]
        public DnsRecordType[] Type = [DnsRecordType.A];
        /// <summary>
        /// <para type="description">Predefined provider(s) (DnsEndpoint) for the query.</para>
        /// <para type="description">When a single provider is specified, the classic single-resolver path is used. When multiple providers are specified, the multi-resolver path is used.</para>
        /// <para type="description">If not specified, the default provider System (UDP) is used.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public DnsEndpoint[] DnsProvider { get; set; } = Array.Empty<DnsEndpoint>();

        /// <summary>
        /// <para type="description">Server to use for the query. If not specified, the default provider System (UDP) is used.</para>
        /// <para type="description">Once a server is specified, the query will be sent to that server.</para>
        /// </summary>
        [Alias("ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public List<string> Server = new List<string>();

        /// <summary>
        /// <para type="description">One or more resolver endpoints in string format. Accepted: "1.1.1.1:53", "[2606:4700:4700::1111]:53", "dns.google:53", or DoH URLs like "https://dns.google/dns-query".</para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = true, ParameterSetName = "PatternResolverEndpoint")]
        public string[] ResolverEndpoint { get; set; } = Array.Empty<string>();

        /// <summary>
        /// <para type="description">One or more predefined providers (DnsEndpoint enum) to expand into endpoints for the multi-resolver.</para>
        /// <para type="description">This enables strategy control (FirstSuccess/FastestWins/SequentialAll) and other multi-resolver options.</para>
        /// </summary>
        [Alias("DnsProviders")]
        [Parameter(Mandatory = true, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = true, ParameterSetName = "PatternResolverDnsProvider")]
        public DnsEndpoint[] ResolverDnsProvider { get; set; } = Array.Empty<DnsEndpoint>();

        /// <summary>
        /// <para type="description">Multi-resolver strategy to use when multiple endpoints are provided.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public MultiResolverStrategy ResolverStrategy { get; set; } = MultiResolverStrategy.FirstSuccess;

        /// <summary>
        /// <para type="description">Limits concurrent queries across endpoints. Defaults to 4.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public int MaxParallelism { get; set; } = 4;

        /// <summary>
        /// <para type="description">Respect endpoint-level timeouts if present. When not set, the cmdlet's -TimeOut value is used.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public SwitchParameter RespectEndpointTimeout { get; set; }

        /// <summary>
        /// <para type="description">Cache duration in minutes for FastestWins strategy.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public int FastestCacheMinutes { get; set; } = 5;

        /// <summary>
        /// <para type="description">Limits concurrent queries per endpoint when using the multi-resolver. Set to a positive value to cap in-flight queries per endpoint; 0 disables the cap.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public int PerEndpointMaxInFlight { get; set; } = 0;

        /// <summary>
        /// <para type="description">Enables response caching based on TTLs for repeated queries of the same (name,type). Disabled by default.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public SwitchParameter ResponseCache { get; set; }

        /// <summary>
        /// <para type="description">Optional override for default cache expiration when TTL is unavailable (seconds).</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public int CacheExpirationSeconds { get; set; } = 0;

        /// <summary>
        /// <para type="description">Minimal TTL allowed for cached entries (seconds). 0 leaves library default.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public int MinCacheTtlSeconds { get; set; } = 0;

        /// <summary>
        /// <para type="description">Maximal TTL allowed for cached entries (seconds). 0 leaves library default.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public int MaxCacheTtlSeconds { get; set; } = 0;

        /// <summary>
        /// <para type="description">If specified, all servers listed in <see cref="Server"/> are queried sequentially and the responses are aggregated in server order.</para>
        /// <para type="description">When not specified, only the first server is queried for faster results.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter AllServers;

        /// <summary>
        /// <para type="description">If specified, the cmdlet sequentially queries each server until a successful response is received.</para>
        /// <para type="description">This option stops on the first server that returns <c>DnsResponseCode.NoError</c>.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter Fallback;

        /// <summary>
        /// <para type="description">If specified, the order of servers defined in <see cref="Server"/> is randomized before querying.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter RandomServer;
        /// <summary>
        /// <para type="description">Provides the full response of the query. If not specified, only the minimal response is provided (just the answer).</para>
        /// <para type="description">If specified, the full response is provided (answer, authority, and additional sections).</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter FullResponse;

        /// <summary>
        /// <para type="description">When set, attempts to parse answers into typed record objects.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter TypedRecords;

        /// <summary>
        /// Gets or sets a value indicating whether TXT records should be
        /// parsed into specialized types (DMARC, SPF, etc.) when <see cref="TypedRecords"/> is
        /// specified. When false, returns simple TXT records.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter ParseTypedTxtRecords;

        /// <summary>
        /// <para type="description">Specifies the timeout for the DNS query, in milliseconds. If the DNS server does not respond within this time, the query will fail. Default is 2000 ms (2 seconds) as defined by <see cref="Configuration.DefaultTimeout"/>. Increase this value for slow networks or unreliable servers.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public int TimeOut = Configuration.DefaultTimeout;

        /// <summary>
        /// <para type="description">Number of retry attempts on transient errors.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public int RetryCount = 3;

        /// <summary>
        /// <para type="description">Delay between retry attempts in milliseconds.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        public int RetryDelayMs = 200;

        /// <summary>
        /// <para type="description">Request DNSSEC data (sets the DO bit).</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter RequestDnsSec;

        /// <summary>
        /// <para type="description">Validate DNSSEC signatures. Implies requesting DNSSEC data.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public SwitchParameter ValidateDnsSec;

        private InternalLogger? _logger;

        private static readonly MethodInfo _isTransientResponse = typeof(ClientX).GetMethod("IsTransientResponse", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _isTransientException = typeof(ClientX).GetMethod("IsTransient", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static bool IsTransientResponse(DnsResponse response) => (bool)_isTransientResponse.Invoke(null, new object[] { response })!;
        private static bool IsTransient(Exception ex) => (bool)_isTransientException.Invoke(null, new object[] { ex })!;

        private async Task<DnsResponse[]> ExecuteWithRetry(Func<Task<DnsResponse[]>> query) {
            DnsResponse[] lastResults = Array.Empty<DnsResponse>();
            Exception? lastException = null;
            for (int attempt = 1; attempt <= RetryCount; attempt++) {
                try {
                    lastResults = await query();
                    if (!lastResults.Any(IsTransientResponse)) {
                        return lastResults;
                    }
                } catch (Exception ex) when (IsTransient(ex)) {
                    lastException = ex;
                }

                if (attempt < RetryCount) {
                    await Task.Delay(RetryDelayMs);
                }
            }

            if (lastException != null) {
                throw lastException;
            }

            return lastResults;
        }

        /// <inheritdoc />
        protected override Task BeginProcessingAsync() {

            // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
            _logger = new InternalLogger(false);
            var internalLoggerPowerShell = new InternalLoggerPowerShell(_logger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
            // var searchEvents = new SearchEvents(internalLogger);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override async Task ProcessRecordAsync() {
            if (TimeOut <= 0) {
                throw new ArgumentOutOfRangeException(nameof(TimeOut), "TimeOut must be greater than zero.");
            }
            if (AllServers.IsPresent && Server.Count == 0) {
                throw new InvalidOperationException("AllServers requires at least one server.");
            }
            var namesToUse = Pattern is null ? Name : ClientX.ExpandPattern(Pattern).ToArray();
            string names = string.Join(", ", namesToUse);
            string types = string.Join(", ", Type);
            bool requestDnsSec = RequestDnsSec.IsPresent || ValidateDnsSec.IsPresent;
            bool validateDnsSec = ValidateDnsSec.IsPresent;
            if (this.ParameterSetName == "ResolverEndpoint" || this.ParameterSetName == "PatternResolverEndpoint" || this.ParameterSetName == "ResolverDnsProvider" || this.ParameterSetName == "PatternResolverDnsProvider" || ((this.ParameterSetName == "DnsProvider" || this.ParameterSetName == "PatternDnsProvider") && DnsProvider != null && DnsProvider.Length > 1)) {
                var namesToUse2 = Pattern is null ? Name : ClientX.ExpandPattern(Pattern).ToArray();
                string names2 = string.Join(", ", namesToUse2);
                string types2 = string.Join(", ", Type);
                bool requestDnsSec2 = RequestDnsSec.IsPresent || ValidateDnsSec.IsPresent;
                bool validateDnsSec2 = ValidateDnsSec.IsPresent; // not used in multi-resolver yet

                DnsResolverEndpoint[] endpoints;
                if (this.ParameterSetName == "ResolverEndpoint" || this.ParameterSetName == "PatternResolverEndpoint") {
                    endpoints = EndpointParser.TryParseMany(ResolverEndpoint, out var errors);
                    if (errors.Count > 0) {
                        foreach (var err in errors) _logger?.WriteError(err);
                        return;
                    }
                } else if (this.ParameterSetName == "ResolverDnsProvider" || this.ParameterSetName == "PatternResolverDnsProvider") {
                    var all = new List<DnsResolverEndpoint>();
                    foreach (var ep in ResolverDnsProvider) {
                        all.AddRange(DnsResolverEndpointFactory.From(ep));
                    }
                    endpoints = all.ToArray();
                } else {
                    var all = new List<DnsResolverEndpoint>();
                    foreach (var ep in DnsProvider) {
                        all.AddRange(DnsResolverEndpointFactory.From(ep));
                    }
                    endpoints = all.ToArray();
                }

                // Apply DO bit preference uniformly when requested
                if (requestDnsSec2) {
                    for (int i = 0; i < endpoints.Length; i++) {
                        endpoints[i] = new DnsResolverEndpoint {
                            Host = endpoints[i].Host,
                            Port = endpoints[i].Port,
                            Family = endpoints[i].Family,
                            Transport = endpoints[i].Transport,
                            Timeout = endpoints[i].Timeout,
                            AllowTcpFallback = endpoints[i].AllowTcpFallback,
                            EdnsBufferSize = endpoints[i].EdnsBufferSize,
                            DnsSecOk = true,
                            DohUrl = endpoints[i].DohUrl
                        };
                    }
                }

                var opts = new MultiResolverOptions {
                    Strategy = ResolverStrategy,
                    MaxParallelism = Math.Max(1, MaxParallelism),
                    RespectEndpointTimeout = RespectEndpointTimeout.IsPresent,
                    DefaultTimeout = TimeSpan.FromMilliseconds(TimeOut),
                    FastestCacheDuration = TimeSpan.FromMinutes(Math.Max(1, FastestCacheMinutes)),
                    PerEndpointMaxInFlight = PerEndpointMaxInFlight > 0 ? PerEndpointMaxInFlight : null,
                    EnableResponseCache = ResponseCache.IsPresent,
                    CacheExpiration = CacheExpirationSeconds > 0 ? TimeSpan.FromSeconds(CacheExpirationSeconds) : null,
                    MinCacheTtl = MinCacheTtlSeconds > 0 ? TimeSpan.FromSeconds(MinCacheTtlSeconds) : null,
                    MaxCacheTtl = MaxCacheTtlSeconds > 0 ? TimeSpan.FromSeconds(MaxCacheTtlSeconds) : null
                };

                IDnsMultiResolver mr = new DnsMultiResolver(endpoints, opts);

                foreach (var t in Type) {
                    _logger?.WriteVerbose("Querying DNS for {0} with type {1} across {2} endpoints", names2, t, endpoints.Length);
                    var result = await mr.QueryBatchAsync(namesToUse2, t, this.CancelToken);
                    foreach (var record in result) {
                        if (record.Status == DnsResponseCode.NoError) {
                            _logger?.WriteVerbose("Query successful for {0} with type {1}", string.Join(", ", record.Questions.Select(q => q.OriginalName)), t);
                        } else {
                            _logger?.WriteWarning("Query failed for {0} with type {1}: {2}", string.Join(", ", record.Questions.Select(q => q.OriginalName)), t, record.Error);
                        }
                        if (FullResponse.IsPresent) {
                            WriteObject(record);
                        } else if (TypedRecords.IsPresent && record.TypedAnswers != null) {
                            WriteObject(record.TypedAnswers, true);
                        } else {
                            WriteObject(record.AnswersMinimal);
                        }
                    }
                }
            } else if (Server.Count > 0) {
                var validServers = new List<string>();
                foreach (string serverEntry in Server) {
                    string trimmed = serverEntry.Trim();
                    if (IPAddress.TryParse(trimmed, out _)) {
                        validServers.Add(trimmed);
                    } else {
                    _logger?.WriteError("Malformed server address '{0}'.", serverEntry);
                    }
                }

                if (validServers.Count == 0) {
                    return;
                }

                List<string> serverOrder = validServers.Distinct().ToList();
                if (RandomServer.IsPresent) {
                    var random = new Random();
                    serverOrder = serverOrder.OrderBy(_ => random.Next()).ToList();
                }

                IEnumerable<DnsResponse> results;
                if (AllServers.IsPresent) {
                    if (Fallback.IsPresent && !RandomServer.IsPresent) {
                        var random = new Random();
                        serverOrder = serverOrder.OrderBy(_ => random.Next()).ToList();
                    }
                    var aggregatedResults = new List<DnsResponse>();
                    foreach (string serverName in serverOrder) {
                        _logger?.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, serverName);
                        var result = await ExecuteWithRetry(() => ClientX.QueryDns(namesToUse, Type, serverName, DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: TimeOut, retryOnTransient: false, maxRetries: 1, retryDelayMs: RetryDelayMs, requestDnsSec: requestDnsSec, validateDnsSec: validateDnsSec, typedRecords: TypedRecords.IsPresent, parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent));
                        aggregatedResults.AddRange(result);
                    }
                    results = aggregatedResults;
                } else if (Fallback.IsPresent) {
                    var aggregatedResults = new List<DnsResponse>();
                    foreach (string serverName in serverOrder) {
                        _logger?.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, serverName);
                        var result = await ExecuteWithRetry(() => ClientX.QueryDns(namesToUse, Type, serverName, DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: TimeOut, retryOnTransient: false, maxRetries: 1, retryDelayMs: RetryDelayMs, requestDnsSec: requestDnsSec, validateDnsSec: validateDnsSec, typedRecords: TypedRecords.IsPresent, parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent));
                        aggregatedResults.AddRange(result);
                        if (aggregatedResults.Any(r => r.Status == DnsResponseCode.NoError)) {
                            break;
                        }
                    }
                    results = aggregatedResults;
                } else {
                    string myServer = serverOrder.First();
                    _logger?.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, myServer);
                    var result = await ExecuteWithRetry(() => ClientX.QueryDns(namesToUse, Type, myServer, DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: TimeOut, retryOnTransient: false, maxRetries: 1, retryDelayMs: RetryDelayMs, requestDnsSec: requestDnsSec, validateDnsSec: validateDnsSec, typedRecords: TypedRecords.IsPresent, parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent));
                    results = result;
                }

                foreach (var record in results) {
                    string serverUsed = record.Questions.Length > 0 ? record.Questions[0].HostName ?? string.Empty : string.Empty;
                    if (record.Status == DnsResponseCode.NoError)
                    {
                        _logger?.WriteVerbose("Query successful for {0} with type {1}, {2} (retries {3})", names, types, serverUsed, record.RetryCount);
                    }
                    else
                    {
                        _logger?.WriteWarning("Query failed for {0} with type {1}, {2} and error: {3}", names, types, serverUsed, record.Error);
                    }

                    if (FullResponse.IsPresent) {
                        WriteObject(record);
                    } else if (TypedRecords.IsPresent && record.TypedAnswers != null) {
                        WriteObject(record.TypedAnswers, true);
                    } else {
                        WriteObject(record.AnswersMinimal);
                    }
                }
            } else {
                DnsResponse[] result;
                if (DnsProvider == null || DnsProvider.Length == 0) {
                    _logger?.WriteVerbose("Querying DNS for {0} with type {1} and provider {2}", names, types, "Default");
                    result = await ExecuteWithRetry(() => ClientX.QueryDns(namesToUse, Type, timeOutMilliseconds: TimeOut, retryOnTransient: false, maxRetries: 1, retryDelayMs: RetryDelayMs, requestDnsSec: requestDnsSec, validateDnsSec: validateDnsSec, typedRecords: TypedRecords.IsPresent, parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent));
                } else {
                    var provider = DnsProvider[0];
                    _logger?.WriteVerbose("Querying DNS for {0} with type {1} and provider {2}", names, types, provider);
                    result = await ExecuteWithRetry(() => ClientX.QueryDns(namesToUse, Type, provider, timeOutMilliseconds: TimeOut, retryOnTransient: false, maxRetries: 1, retryDelayMs: RetryDelayMs, requestDnsSec: requestDnsSec, validateDnsSec: validateDnsSec, typedRecords: TypedRecords.IsPresent, parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent));
                }

                foreach (var record in result) {
                    if (record.Status == DnsResponseCode.NoError)
                    {
                        string providerLabel = (DnsProvider != null && DnsProvider.Length > 0) ? DnsProvider[0].ToString() : "Default";
                        _logger?.WriteVerbose("Query successful for {0} with type {1}, {2} (retries {3})", names, types, providerLabel, record.RetryCount);
                    } else {
                        string providerLabel = (DnsProvider != null && DnsProvider.Length > 0) ? DnsProvider[0].ToString() : "Default";
                        _logger?.WriteWarning("Query failed for {0} with type {1}, {2} and error: {3}", names, types, providerLabel, record.Error);
                    }
                    if (FullResponse.IsPresent) {
                        WriteObject(record);
                    } else if (TypedRecords.IsPresent && record.TypedAnswers != null) {
                        WriteObject(record.TypedAnswers, true);
                    } else {
                        WriteObject(record.AnswersMinimal);
                    }
                }
            }

            return;
        }
    }
}
