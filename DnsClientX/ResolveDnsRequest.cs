using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace DnsClientX {
    /// <summary>
    /// Represents a reusable request model for DNS queries across single-resolver and multi-resolver flows.
    /// </summary>
    public sealed class ResolveDnsRequest {
        /// <summary>
        /// Gets or sets the explicit DNS names to query.
        /// </summary>
        public string[] Names { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets an optional name pattern that expands into multiple DNS names.
        /// </summary>
        public string? Pattern { get; set; }

        /// <summary>
        /// Gets or sets the record types to query.
        /// </summary>
        public DnsRecordType[] RecordTypes { get; set; } = [DnsRecordType.A];

        /// <summary>
        /// Gets or sets the predefined provider inputs for single-provider or multi-provider execution.
        /// </summary>
        public DnsEndpoint[] DnsProviders { get; set; } = Array.Empty<DnsEndpoint>();

        /// <summary>
        /// Gets or sets how a built-in provider chooses among its backing resolvers.
        /// </summary>
        public DnsSelectionStrategy DnsSelectionStrategy { get; set; } = DnsSelectionStrategy.First;

        /// <summary>
        /// Gets or sets the explicit server names or IP addresses to query.
        /// </summary>
        public string[] Servers { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the raw resolver endpoints used by the multi-resolver flow.
        /// </summary>
        public string[] ResolverEndpoints { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the provider inputs that should be expanded into multi-resolver endpoints.
        /// </summary>
        public DnsEndpoint[] ResolverDnsProviders { get; set; } = Array.Empty<DnsEndpoint>();

        /// <summary>
        /// Gets or sets the multi-resolver strategy.
        /// </summary>
        public MultiResolverStrategy ResolverStrategy { get; set; } = MultiResolverStrategy.FirstSuccess;

        /// <summary>
        /// Gets or sets the maximum number of concurrent endpoint queries for multi-resolver execution.
        /// </summary>
        public int MaxParallelism { get; set; } = 4;

        /// <summary>
        /// Gets or sets whether endpoint-specific timeouts should be respected in the multi-resolver.
        /// </summary>
        public bool RespectEndpointTimeout { get; set; }

        /// <summary>
        /// Gets or sets the duration in minutes used by the FastestWins endpoint cache.
        /// </summary>
        public int FastestCacheMinutes { get; set; } = 5;

        /// <summary>
        /// Gets or sets the optional cap on concurrent in-flight queries per multi-resolver endpoint.
        /// </summary>
        public int PerEndpointMaxInFlight { get; set; }

        /// <summary>
        /// Gets or sets whether multi-resolver response caching is enabled.
        /// </summary>
        public bool ResponseCache { get; set; }

        /// <summary>
        /// Gets or sets the fallback cache expiration in seconds when TTL is unavailable.
        /// </summary>
        public int CacheExpirationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the minimum cache TTL in seconds.
        /// </summary>
        public int MinCacheTtlSeconds { get; set; }

        /// <summary>
        /// Gets or sets the maximum cache TTL in seconds.
        /// </summary>
        public int MaxCacheTtlSeconds { get; set; }

        /// <summary>
        /// Gets or sets whether all configured servers should be queried sequentially.
        /// </summary>
        public bool AllServers { get; set; }

        /// <summary>
        /// Gets or sets whether configured servers should be queried until one succeeds.
        /// </summary>
        public bool Fallback { get; set; }

        /// <summary>
        /// Gets or sets whether configured servers should be randomized before use.
        /// </summary>
        public bool RandomServer { get; set; }

        /// <summary>
        /// Gets or sets the timeout in milliseconds.
        /// </summary>
        public int TimeOutMilliseconds { get; set; } = Configuration.DefaultTimeout;

        /// <summary>
        /// Gets or sets the number of retry attempts on transient failures.
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay between retry attempts in milliseconds.
        /// </summary>
        public int RetryDelayMs { get; set; } = 200;

        /// <summary>
        /// Gets or sets whether DNSSEC data should be requested.
        /// </summary>
        public bool RequestDnsSec { get; set; }

        /// <summary>
        /// Gets or sets whether DNSSEC validation should be performed.
        /// </summary>
        public bool ValidateDnsSec { get; set; }

        /// <summary>
        /// Gets or sets whether typed records should be projected for successful answers.
        /// </summary>
        public bool TypedRecords { get; set; }

        /// <summary>
        /// Gets or sets whether TXT answers should be parsed into specialized typed records.
        /// </summary>
        public bool ParseTypedTxtRecords { get; set; }

        /// <summary>
        /// Gets or sets whether EDNS should be enabled.
        /// </summary>
        public bool EnableEdns { get; set; }

        /// <summary>
        /// Gets or sets the EDNS UDP buffer size.
        /// </summary>
        public int EdnsBufferSize { get; set; }

        /// <summary>
        /// Gets or sets the EDNS client subnet in CIDR notation.
        /// </summary>
        public string? ClientSubnet { get; set; }

        /// <summary>
        /// Gets or sets whether the CD bit should be set on outgoing queries.
        /// </summary>
        public bool CheckingDisabled { get; set; }

        /// <summary>
        /// Gets or sets whether the NSID EDNS option should be requested.
        /// </summary>
        public bool RequestNsid { get; set; }

        /// <summary>
        /// Gets or sets the request format used by explicit server execution.
        /// </summary>
        public DnsRequestFormat RequestFormat { get; set; } = DnsRequestFormat.DnsOverUDP;

        /// <summary>
        /// Gets or sets an optional port override used by explicit server execution.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets an optional User-Agent for HTTP-based transports.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Gets or sets an optional preferred HTTP version.
        /// </summary>
        public Version? HttpVersion { get; set; }

        /// <summary>
        /// Gets or sets whether TLS certificate validation errors should be ignored.
        /// </summary>
        public bool IgnoreCertificateErrors { get; set; }

        /// <summary>
        /// Gets or sets whether UDP transport may fall back to TCP when truncated.
        /// </summary>
        public bool UseTcpFallback { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional proxy URI for HTTP-based transports.
        /// </summary>
        public Uri? ProxyUri { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of HTTP connections per server.
        /// </summary>
        public int MaxConnectionsPerServer { get; set; }

        /// <summary>
        /// Gets or sets an optional client-side concurrency cap for individual resolver instances.
        /// </summary>
        public int MaxConcurrency { get; set; }

        /// <summary>
        /// Gets a value indicating whether this request should execute through the multi-resolver.
        /// </summary>
        public bool IsMultiResolverRequest =>
            ResolverEndpoints.Length > 0 ||
            ResolverDnsProviders.Length > 0 ||
            DnsProviders.Length > 1;

        /// <summary>
        /// Gets a value indicating whether DNSSEC data must be requested.
        /// </summary>
        public bool ShouldRequestDnsSec => RequestDnsSec || ValidateDnsSec;

        /// <summary>
        /// Gets a value indicating whether DNSSEC validation should be performed.
        /// </summary>
        public bool ShouldValidateDnsSec => ValidateDnsSec;

        internal int EffectiveMaxConnectionsPerServer =>
            MaxConnectionsPerServer > 0 ? MaxConnectionsPerServer : Configuration.DefaultMaxConnectionsPerServer;

        internal int? EffectiveMaxConcurrency =>
            MaxConcurrency > 0 ? MaxConcurrency : null;

        /// <summary>
        /// Validates the request before execution.
        /// </summary>
        public void Validate() {
            bool hasPattern = !string.IsNullOrWhiteSpace(Pattern);
            bool hasNames = Names is { Length: > 0 };

            if (hasPattern == hasNames) {
                throw new InvalidOperationException("Specify either Names or Pattern.");
            }

            if (hasNames && Names.Any(string.IsNullOrWhiteSpace)) {
                throw new ArgumentException("Names cannot contain null, empty, or whitespace entries.", nameof(Names));
            }

            if (RecordTypes == null || RecordTypes.Length == 0) {
                throw new ArgumentException("At least one record type must be specified.", nameof(RecordTypes));
            }

            int configuredSources = 0;
            if (Servers.Length > 0) {
                configuredSources++;
            }
            if (ResolverEndpoints.Length > 0) {
                configuredSources++;
            }
            if (ResolverDnsProviders.Length > 0) {
                configuredSources++;
            }
            if (DnsProviders.Length > 0) {
                configuredSources++;
            }

            if (configuredSources > 1) {
                throw new InvalidOperationException("Specify only one resolver source: DnsProviders, Servers, ResolverEndpoints, or ResolverDnsProviders.");
            }

            if (TimeOutMilliseconds <= 0) {
                throw new ArgumentOutOfRangeException(nameof(TimeOutMilliseconds), "TimeOutMilliseconds must be greater than zero.");
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

            if (EdnsBufferSize < 0 || EdnsBufferSize > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(EdnsBufferSize), $"EdnsBufferSize must be between 0 and {ushort.MaxValue}.");
            }

            if (AllServers && Servers.Length == 0) {
                throw new InvalidOperationException("AllServers requires at least one server.");
            }

            if (!string.IsNullOrWhiteSpace(ClientSubnet)) {
                ValidateClientSubnet(ClientSubnet!);
            }
        }

        /// <summary>
        /// Expands the request into concrete DNS names.
        /// </summary>
        public string[] GetExpandedNames() {
            return !string.IsNullOrWhiteSpace(Pattern)
                ? ClientX.ExpandPattern(Pattern!).ToArray()
                : Names;
        }

        internal IWebProxy? CreateWebProxy() {
            return ProxyUri is null ? null : new WebProxy(ProxyUri);
        }

        internal EdnsOptions? CreateEdnsOptions() {
            bool hasSubnet = !string.IsNullOrWhiteSpace(ClientSubnet);
            bool enableEdns = EnableEdns || hasSubnet || EdnsBufferSize > 0 || RequestNsid;
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

            if (RequestNsid) {
                options.Options.Add(new NsidOption());
            }

            return options;
        }

        private static void ValidateClientSubnet(string subnet) {
            string[] parts = subnet.Split('/');
            if (parts.Length is < 1 or > 2 || !IPAddress.TryParse(parts[0], out var ipAddress)) {
                throw new ArgumentException("ClientSubnet must be a valid IP address or CIDR subnet.", nameof(ClientSubnet));
            }

            int maxPrefixLength = ipAddress.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
            if (parts.Length == 1) {
                return;
            }

            if (!int.TryParse(parts[1], out int prefixLength) || prefixLength < 0 || prefixLength > maxPrefixLength) {
                throw new ArgumentException($"ClientSubnet prefix length must be between 0 and {maxPrefixLength} for {ipAddress.AddressFamily}.", nameof(ClientSubnet));
            }
        }
    }
}
