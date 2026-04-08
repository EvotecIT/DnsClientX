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
    ///  <para>Modern endpoints (DoQ + DoH3) through the shared endpoint parser</para>
    ///  <code>Resolve-Dns -Name 'example.com' -Type A -ResolverEndpoint 'doq@dns.quad9.net:853','doh3@https://dns.quad9.net/dns-query' -ResolverStrategy FirstSuccess</code>
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
    /// <example>
    ///  <para>Reuse the recommended resolver from a saved selection snapshot</para>
    ///  <code>Resolve-Dns -Name 'example.com' -Type A -ResolverSelectionPath '.\resolver-score.json'</code>
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
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ResolverSelection")]
        public string[] Name { get; set; } = Array.Empty<string>();

        /// <summary>
        /// <para type="description">Pattern to expand into multiple DNS queries.</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternDnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternServerName")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternResolverEndpoint")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternResolverDnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PatternResolverSelection")]
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
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ResolverSelection")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "PatternResolverSelection")]
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
        /// <para type="description">One or more resolver endpoints in string format. Accepted: "1.1.1.1:53", "[2606:4700:4700::1111]:53", "dns.google:53", DoH URLs like "https://dns.google/dns-query", and transport-prefixed values such as "doq@dns.quad9.net:853" or "doh3@https://dns.quad9.net/dns-query".</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        public string[] ResolverEndpoint { get; set; } = Array.Empty<string>();

        /// <summary>
        /// <para type="description">One or more files containing resolver endpoints for the multi-resolver. Blank lines and full-line comments are ignored.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        public string[] ResolverEndpointFile { get; set; } = Array.Empty<string>();

        /// <summary>
        /// <para type="description">One or more HTTP or HTTPS URLs exposing resolver endpoints for the multi-resolver.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "PatternResolverEndpoint")]
        public string[] ResolverEndpointUrl { get; set; } = Array.Empty<string>();

        /// <summary>
        /// <para type="description">One or more predefined providers (DnsEndpoint enum) to expand into endpoints for the multi-resolver.</para>
        /// <para type="description">This enables strategy control (FirstSuccess/FastestWins/SequentialAll) and other multi-resolver options.</para>
        /// </summary>
        [Alias("DnsProviders")]
        [Parameter(Mandatory = true, ParameterSetName = "ResolverDnsProvider")]
        [Parameter(Mandatory = true, ParameterSetName = "PatternResolverDnsProvider")]
        public DnsEndpoint[] ResolverDnsProvider { get; set; } = Array.Empty<DnsEndpoint>();

        /// <summary>
        /// <para type="description">Path to a saved resolver score snapshot whose recommended resolver should be reused for the query.</para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "ResolverSelection")]
        [Parameter(Mandatory = true, ParameterSetName = "PatternResolverSelection")]
        public string ResolverSelectionPath { get; set; } = string.Empty;

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

        private void WriteResponse(DnsResponse record) {
            if (FullResponse.IsPresent) {
                WriteObject(record);
            } else if (TypedRecords.IsPresent && record.TypedAnswers != null) {
                WriteObject(record.TypedAnswers, true);
            } else {
                WriteObject(record.AnswersMinimal);
            }
        }

        private ResolveDnsRequest CreateRequest() {
            return new ResolveDnsRequest {
                Names = Name,
                Pattern = Pattern,
                RecordTypes = Type,
                DnsProviders = DnsProvider ?? Array.Empty<DnsEndpoint>(),
                DnsSelectionStrategy = DnsSelectionStrategy,
                Servers = Server?.ToArray() ?? Array.Empty<string>(),
                ResolverEndpoints = ResolverEndpoint ?? Array.Empty<string>(),
                ResolverEndpointFiles = ResolverEndpointFile ?? Array.Empty<string>(),
                ResolverEndpointUrls = ResolverEndpointUrl ?? Array.Empty<string>(),
                ResolverDnsProviders = ResolverDnsProvider ?? Array.Empty<DnsEndpoint>(),
                ResolverSelectionPath = ResolverSelectionPath,
                ResolverStrategy = ResolverStrategy,
                MaxParallelism = MaxParallelism,
                RespectEndpointTimeout = RespectEndpointTimeout.IsPresent,
                FastestCacheMinutes = FastestCacheMinutes,
                PerEndpointMaxInFlight = PerEndpointMaxInFlight,
                ResponseCache = ResponseCache.IsPresent,
                CacheExpirationSeconds = CacheExpirationSeconds,
                MinCacheTtlSeconds = MinCacheTtlSeconds,
                MaxCacheTtlSeconds = MaxCacheTtlSeconds,
                AllServers = AllServers.IsPresent,
                Fallback = Fallback.IsPresent,
                RandomServer = RandomServer.IsPresent,
                TimeOutMilliseconds = TimeOut,
                RetryCount = RetryCount,
                RetryDelayMs = RetryDelayMs,
                RequestDnsSec = RequestDnsSec.IsPresent,
                ValidateDnsSec = ValidateDnsSec.IsPresent,
                TypedRecords = TypedRecords.IsPresent,
                ParseTypedTxtRecords = ParseTypedTxtRecords.IsPresent,
                EnableEdns = EnableEdns.IsPresent,
                EdnsBufferSize = EdnsBufferSize,
                ClientSubnet = ClientSubnet,
                CheckingDisabled = CheckingDisabled.IsPresent,
                RequestNsid = RequestNsid.IsPresent,
                RequestFormat = RequestFormat,
                Port = Port,
                UserAgent = UserAgent,
                HttpVersion = HttpVersion,
                IgnoreCertificateErrors = IgnoreCertificateErrors.IsPresent,
                UseTcpFallback = UseTcpFallback,
                ProxyUri = ProxyUri,
                MaxConnectionsPerServer = MaxConnectionsPerServer,
                MaxConcurrency = MaxConcurrency
            };
        }

        private void ValidateResolverEndpointInputs() {
            if (ParameterSetName != "ResolverEndpoint" && ParameterSetName != "PatternResolverEndpoint") {
                return;
            }

            bool hasInlineEndpoints = ResolverEndpoint is { Length: > 0 };
            bool hasFiles = ResolverEndpointFile is { Length: > 0 };
            bool hasUrls = ResolverEndpointUrl is { Length: > 0 };

            if (!hasInlineEndpoints && !hasFiles && !hasUrls) {
                throw new PSArgumentException(
                    "At least one resolver endpoint, resolver endpoint file, or resolver endpoint URL must be specified.",
                    nameof(ResolverEndpoint));
            }
        }

        private void LogResponse(DnsResponse record) {
            string names = string.Join(", ", record.Questions.Select(q => q.OriginalName ?? q.Name));
            string types = string.Join(", ", record.Questions.Select(q => q.Type).Distinct());
            string source = record.Questions.Length > 0 ? record.Questions[0].HostName ?? string.Empty : string.Empty;

            if (record.Status == DnsResponseCode.NoError) {
                WriteVerbose($"Query successful for {names} with type {types}, {source} (retries {record.RetryCount})");
            } else {
                WriteWarning($"Query failed for {names} with type {types}, {source} and error: {record.Error}");
            }
        }

        /// <inheritdoc />
        protected override async Task ProcessRecordAsync() {
            ValidateResolverEndpointInputs();
            var request = CreateRequest();

            try {
                var result = await ClientX.QueryDns(request, CancelToken).ConfigureAwait(false);
                foreach (var record in result) {
                    LogResponse(record);
                    WriteResponse(record);
                }
            } catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) {
                WriteError(new ErrorRecord(ex, "ResolveDnsInvalidInput", ErrorCategory.InvalidArgument, request));
            }
        }
    }
}
