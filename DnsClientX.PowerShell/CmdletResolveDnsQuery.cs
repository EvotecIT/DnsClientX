using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Resolves DNS records (A, AAAA, MX, TXT, …) over UDP, TCP, DoT, DoH, QUIC, or multicast with optional multi-resolver strategies.</para>
    /// <para type="description">Supports single-provider queries, explicit servers with transport selection, multiple providers with FirstSuccess/FastestWins/SequentialAll/RoundRobin, direct resolver endpoints, DNSSEC, EDNS/ECS, concurrency control, and TTL-based response caching.</para>
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
    /// <example>
    ///  <para>Query a specific server over DoH with explicit transport settings</para>
    ///  <code>Resolve-Dns -Name 'example.com' -Type A -Server 'dns.google' -RequestFormat DnsOverHttps -Port 443 -UserAgent 'DnsClientX/PowerShell' -HttpVersion 2.0</code>
    /// </example>
    /// <example>
    ///  <para>Send EDNS client subnet and request NSID metadata</para>
    ///  <code>Resolve-Dns -Name 'example.com' -Type A -DnsProvider Quad9ECS -EnableEdns -ClientSubnet '192.0.2.0/24' -RequestNsid -FullResponse</code>
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
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ResolverDnsProvider")]
        public string[] Name { get; set; } = Array.Empty<string>();

        /// <summary>
        /// <para type="description">Pattern to expand into multiple DNS queries.</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternResolverEndpoint")]
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
        /// <para type="description">How to choose among built-in provider hostnames when a single provider exposes multiple backends.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public DnsSelectionStrategy DnsSelectionStrategy { get; set; } = DnsSelectionStrategy.First;

        /// <summary>
        /// <para type="description">Server to use for the query. If not specified, the default provider System (UDP) is used.</para>
        /// <para type="description">Once a server is specified, the query will be sent to that server.</para>
        /// </summary>
        [Alias("ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [ValidateNotNull]
        public List<string> Server { get; set; } = new List<string>();

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
        [Parameter(Mandatory = false)]
        public SwitchParameter FullResponse;

        /// <summary>
        /// <para type="description">When set, attempts to parse answers into typed record objects.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false)]
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
        [Parameter(Mandatory = false)]
        public SwitchParameter ParseTypedTxtRecords;

        /// <summary>
        /// <para type="description">Specifies the timeout for the DNS query, in milliseconds. If the DNS server does not respond within this time, the query will fail. Default is 2000 ms (2 seconds) as defined by <see cref="Configuration.DefaultTimeout"/>. Increase this value for slow networks or unreliable servers.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false)]
        public int TimeOut = Configuration.DefaultTimeout;

        /// <summary>
        /// <para type="description">Number of retry attempts on transient errors.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false)]
        public int RetryCount = 3;

        /// <summary>
        /// <para type="description">Delay between retry attempts in milliseconds.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false)]
        public int RetryDelayMs = 200;

        /// <summary>
        /// <para type="description">Request DNSSEC data (sets the DO bit).</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false)]
        public SwitchParameter RequestDnsSec;

        /// <summary>
        /// <para type="description">Validate DNSSEC signatures. Implies requesting DNSSEC data.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = false)]
        public SwitchParameter ValidateDnsSec;

        /// <summary>
        /// <para type="description">Enables EDNS on outgoing queries.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter EnableEdns;

        /// <summary>
        /// <para type="description">Sets the EDNS UDP buffer size. When specified, EDNS is enabled automatically.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public int EdnsBufferSize { get; set; }

        /// <summary>
        /// <para type="description">Sets the EDNS Client Subnet (ECS) in CIDR notation, for example 192.0.2.0/24.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? ClientSubnet { get; set; }

        /// <summary>
        /// <para type="description">Sets the CD (checking disabled) bit on outgoing queries.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter CheckingDisabled;

        /// <summary>
        /// <para type="description">Requests the NSID EDNS option so compatible servers may include resolver identity metadata in the response.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter RequestNsid;

        /// <summary>
        /// <para type="description">Explicit request format for the -Server path, such as DnsOverUDP, DnsOverTCP, DnsOverTLS, or DnsOverHttps.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public DnsRequestFormat RequestFormat { get; set; } = DnsRequestFormat.DnsOverUDP;

        /// <summary>
        /// <para type="description">Optional port override for the -Server path. If omitted, the selected request format decides the default port.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ServerName")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternServerName")]
        public int Port { get; set; }

        /// <summary>
        /// <para type="description">Optional User-Agent header for HTTP-based transports.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// <para type="description">Optional preferred HTTP protocol version, for example 2.0 or 3.0.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public Version? HttpVersion { get; set; }

        /// <summary>
        /// <para type="description">Ignore TLS certificate validation errors.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter IgnoreCertificateErrors { get; set; }

        /// <summary>
        /// <para type="description">Controls whether UDP queries may fall back to TCP when truncated.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public bool UseTcpFallback { get; set; } = true;

        /// <summary>
        /// <para type="description">Optional web proxy URI for HTTP-based transports.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public Uri? ProxyUri { get; set; }

        /// <summary>
        /// <para type="description">Maximum HTTP connections allowed per server. When 0, the library default is used.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public int MaxConnectionsPerServer { get; set; }

        /// <summary>
        /// <para type="description">Optional cap on client-side query concurrency for single-resolver operations. When 0, the library default is used.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public int MaxConcurrency { get; set; }

        private InternalLogger? _logger;

        private bool IsMultiResolverParameterSet =>
            this.ParameterSetName == "ResolverEndpoint" ||
            this.ParameterSetName == "PatternResolverEndpoint" ||
            this.ParameterSetName == "ResolverDnsProvider" ||
            this.ParameterSetName == "PatternResolverDnsProvider" ||
            ((this.ParameterSetName == "DnsProvider" || this.ParameterSetName == "PatternDnsProvider") && DnsProvider is { Length: > 1 });

        private bool ShouldRequestDnsSec => RequestDnsSec.IsPresent || ValidateDnsSec.IsPresent;
        private bool ShouldValidateDnsSec => ValidateDnsSec.IsPresent;
        private bool HasBoundParameter(string name) => MyInvocation.BoundParameters.ContainsKey(name);

        private IWebProxy? CreateWebProxy() {
            if (!HasBoundParameter(nameof(ProxyUri)) || ProxyUri is null) {
                return null;
            }

            return new WebProxy(ProxyUri);
        }

        private int EffectiveMaxConnectionsPerServer =>
            MaxConnectionsPerServer > 0 ? MaxConnectionsPerServer : Configuration.DefaultMaxConnectionsPerServer;

        private int? EffectiveMaxConcurrency =>
            MaxConcurrency > 0 ? MaxConcurrency : null;

        private EdnsOptions? CreateEdnsOptions() {
            if (EdnsBufferSize < 0) {
                throw new ArgumentOutOfRangeException(nameof(EdnsBufferSize), "EdnsBufferSize cannot be negative.");
            }

            bool hasSubnet = !string.IsNullOrWhiteSpace(ClientSubnet);
            bool enableEdns = EnableEdns.IsPresent || hasSubnet || EdnsBufferSize > 0 || RequestNsid.IsPresent;
            if (!enableEdns) {
                return null;
            }

            var options = new EdnsOptions {
                EnableEdns = true
            };

            if (EdnsBufferSize > 0) {
                options.UdpBufferSize = EdnsBufferSize;
            }

            if (hasSubnet) {
                options.Subnet = new EdnsClientSubnetOption(ClientSubnet!);
            }

            if (RequestNsid.IsPresent) {
                options.Options.Add(new NsidOption());
            }

            return options;
        }

        private void ApplyAdvancedConfiguration(ClientX client, EdnsOptions? ednsOptions) {
            client.EndpointConfiguration.TimeOut = TimeOut;
            client.EndpointConfiguration.CheckingDisabled = CheckingDisabled.IsPresent;
            client.EndpointConfiguration.MaxConcurrency = EffectiveMaxConcurrency;
            if (ednsOptions != null) {
                client.EndpointConfiguration.EdnsOptions = ednsOptions;
            }
        }

        private ClientX CreateClientForServer(string serverName) {
            var client = new ClientX(
                serverName,
                RequestFormat,
                timeOutMilliseconds: TimeOut,
                userAgent: UserAgent,
                httpVersion: HttpVersion,
                ignoreCertificateErrors: IgnoreCertificateErrors.IsPresent,
                enableCache: false,
                useTcpFallback: UseTcpFallback,
                webProxy: CreateWebProxy(),
                maxConnectionsPerServer: EffectiveMaxConnectionsPerServer);

            if (Port > 0) {
                client.EndpointConfiguration.Port = Port;
            }

            return client;
        }

        private ClientX CreateClientForProvider(DnsEndpoint provider) {
            return new ClientX(
                provider,
                DnsSelectionStrategy,
                TimeOut,
                UserAgent,
                HttpVersion,
                IgnoreCertificateErrors.IsPresent,
                enableCache: false,
                useTcpFallback: UseTcpFallback,
                webProxy: CreateWebProxy(),
                maxConnectionsPerServer: EffectiveMaxConnectionsPerServer);
        }

        private static bool IsValidServerName(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            return IPAddress.TryParse(value, out _) || Uri.CheckHostName(value) != UriHostNameType.Unknown;
        }

        private async Task<DnsResponse[]> QueryWithClientAsync(ClientX client, string[] namesToUse) {
            var responses = new List<DnsResponse>();
            foreach (var recordType in Type) {
                var result = await client.Resolve(
                    namesToUse,
                    recordType,
                    requestDnsSec: ShouldRequestDnsSec,
                    validateDnsSec: ShouldValidateDnsSec,
                    returnAllTypes: false,
                    retryOnTransient: false,
                    maxRetries: 1,
                    retryDelayMs: RetryDelayMs,
                    typedRecords: TypedRecords.IsPresent,
                    parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent,
                    cancellationToken: CancelToken).ConfigureAwait(false);
                responses.AddRange(result);
            }

            return responses.ToArray();
        }

        private void WriteResponse(DnsResponse record) {
            if (FullResponse.IsPresent) {
                WriteObject(record);
            } else if (TypedRecords.IsPresent && record.TypedAnswers != null) {
                WriteObject(record.TypedAnswers, true);
            } else {
                WriteObject(record.AnswersMinimal);
            }
        }

        private async Task<DnsResponse[]> ExecuteWithRetry(Func<Task<DnsResponse[]>> query) {
            DnsResponse[] lastResults = Array.Empty<DnsResponse>();
            Exception? lastException = null;
            for (int attempt = 1; attempt <= RetryCount; attempt++) {
                try {
                    lastResults = await query();
                    if (!lastResults.Any(DnsQueryDiagnostics.IsTransient)) {
                        return lastResults;
                    }
                } catch (Exception ex) when (DnsQueryDiagnostics.IsTransient(ex)) {
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
            if (RetryCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(RetryCount), "RetryCount must be greater than zero.");
            }
            if (RetryDelayMs < 0) {
                throw new ArgumentOutOfRangeException(nameof(RetryDelayMs), "RetryDelayMs cannot be negative.");
            }
            if (Port < 0 || Port > 65535) {
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 0 and 65535.");
            }
            if (MaxConnectionsPerServer < 0) {
                throw new ArgumentOutOfRangeException(nameof(MaxConnectionsPerServer), "MaxConnectionsPerServer cannot be negative.");
            }
            if (MaxConcurrency < 0) {
                throw new ArgumentOutOfRangeException(nameof(MaxConcurrency), "MaxConcurrency cannot be negative.");
            }
            if (AllServers.IsPresent && Server.Count == 0) {
                throw new InvalidOperationException("AllServers requires at least one server.");
            }

            var namesToUse = Pattern is null ? Name : ClientX.ExpandPattern(Pattern).ToArray();
            string names = string.Join(", ", namesToUse);
            string types = string.Join(", ", Type);
            var ednsOptions = CreateEdnsOptions();

            if (IsMultiResolverParameterSet) {
                DnsResolverEndpoint[] endpoints;
                if (this.ParameterSetName == "ResolverEndpoint" || this.ParameterSetName == "PatternResolverEndpoint") {
                    endpoints = EndpointParser.TryParseMany(ResolverEndpoint, out var errors);
                    if (errors.Count > 0) {
                        foreach (var err in errors) {
                            _logger?.WriteError(err);
                        }
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

                if (endpoints.Length == 0) {
                    _logger?.WriteWarning("No endpoints were produced from the specified resolver inputs.");
                    return;
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
                    MaxCacheTtl = MaxCacheTtlSeconds > 0 ? TimeSpan.FromSeconds(MaxCacheTtlSeconds) : null,
                    RequestDnsSec = ShouldRequestDnsSec,
                    ValidateDnsSec = ShouldValidateDnsSec,
                    TypedRecords = TypedRecords.IsPresent,
                    ParseTypedTxtRecords = ParseTypedTxtRecords.IsPresent,
                    CheckingDisabled = CheckingDisabled.IsPresent,
                    EdnsOptions = ednsOptions,
                    UserAgent = UserAgent,
                    HttpVersion = HttpVersion,
                    IgnoreCertificateErrors = IgnoreCertificateErrors.IsPresent,
                    UseTcpFallback = UseTcpFallback,
                    WebProxy = CreateWebProxy(),
                    MaxConnectionsPerServer = EffectiveMaxConnectionsPerServer,
                    MaxConcurrency = EffectiveMaxConcurrency
                };

                using var mr = new DnsMultiResolver(endpoints, opts);
                foreach (var recordType in Type) {
                    _logger?.WriteVerbose("Querying DNS for {0} with type {1} across {2} endpoints", names, recordType, endpoints.Length);
                    var result = await mr.QueryBatchAsync(namesToUse, recordType, this.CancelToken).ConfigureAwait(false);
                    foreach (var record in result) {
                        if (record.Status == DnsResponseCode.NoError) {
                            _logger?.WriteVerbose("Query successful for {0} with type {1}", string.Join(", ", record.Questions.Select(q => q.OriginalName)), recordType);
                        } else {
                            _logger?.WriteWarning("Query failed for {0} with type {1}: {2}", string.Join(", ", record.Questions.Select(q => q.OriginalName)), recordType, record.Error);
                        }
                        WriteResponse(record);
                    }
                }
            } else if (Server.Count > 0) {
                var validServers = new List<string>();
                foreach (string serverEntry in Server) {
                    string trimmed = serverEntry.Trim();
                    if (IsValidServerName(trimmed)) {
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
                        var result = await ExecuteWithRetry(async () => {
                            using var client = CreateClientForServer(serverName);
                            ApplyAdvancedConfiguration(client, ednsOptions);
                            return await QueryWithClientAsync(client, namesToUse).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                        aggregatedResults.AddRange(result);
                    }
                    results = aggregatedResults;
                } else if (Fallback.IsPresent) {
                    var aggregatedResults = new List<DnsResponse>();
                    foreach (string serverName in serverOrder) {
                        _logger?.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, serverName);
                        var result = await ExecuteWithRetry(async () => {
                            using var client = CreateClientForServer(serverName);
                            ApplyAdvancedConfiguration(client, ednsOptions);
                            return await QueryWithClientAsync(client, namesToUse).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                        aggregatedResults.AddRange(result);
                        if (aggregatedResults.Any(r => r.Status == DnsResponseCode.NoError)) {
                            break;
                        }
                    }
                    results = aggregatedResults;
                } else {
                    string myServer = serverOrder.First();
                    _logger?.WriteVerbose("Querying DNS for {0} with type {1}, {2}", names, types, myServer);
                    var result = await ExecuteWithRetry(async () => {
                        using var client = CreateClientForServer(myServer);
                        ApplyAdvancedConfiguration(client, ednsOptions);
                        return await QueryWithClientAsync(client, namesToUse).ConfigureAwait(false);
                    }).ConfigureAwait(false);
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

                    WriteResponse(record);
                }
            } else {
                DnsResponse[] result;
                DnsEndpoint provider = DnsProvider == null || DnsProvider.Length == 0 ? DnsEndpoint.System : DnsProvider[0];
                _logger?.WriteVerbose("Querying DNS for {0} with type {1} and provider {2}", names, types, provider);
                if (provider == DnsEndpoint.RootServer) {
                    result = await ExecuteWithRetry(() => ClientX.QueryDns(
                        namesToUse,
                        Type,
                        provider,
                        timeOutMilliseconds: TimeOut,
                        retryOnTransient: false,
                        maxRetries: 1,
                        retryDelayMs: RetryDelayMs,
                        requestDnsSec: ShouldRequestDnsSec,
                        validateDnsSec: ShouldValidateDnsSec,
                        typedRecords: TypedRecords.IsPresent,
                        parseTypedTxtRecords: ParseTypedTxtRecords.IsPresent,
                        cancellationToken: CancelToken)).ConfigureAwait(false);
                } else {
                    result = await ExecuteWithRetry(async () => {
                        using var client = CreateClientForProvider(provider);
                        ApplyAdvancedConfiguration(client, ednsOptions);
                        return await QueryWithClientAsync(client, namesToUse).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }

                foreach (var record in result) {
                    string providerLabel = provider.ToString();
                    if (record.Status == DnsResponseCode.NoError)
                    {
                        _logger?.WriteVerbose("Query successful for {0} with type {1}, {2} (retries {3})", names, types, providerLabel, record.RetryCount);
                    } else {
                        _logger?.WriteWarning("Query failed for {0} with type {1}, {2} and error: {3}", names, types, providerLabel, record.Error);
                    }
                    WriteResponse(record);
                }
            }

            return;
        }
    }
}
