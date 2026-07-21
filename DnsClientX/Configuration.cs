using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace DnsClientX {
    /// <summary>
    /// Class representing the configuration for a DNS-over-HTTPS endpoint.
    /// </summary>
    /// <remarks>
    /// Instances of this class are used by <see cref="ClientXBuilder"/> to describe the target server and connection settings.
    /// </remarks>
    public class Configuration {
        /// <summary>
        /// Random generator used for hostname selection on frameworks lacking
        /// <c>Random.Shared</c>.
        /// </summary>
#if NET6_0_OR_GREATER
        // Random.Shared provides a threadsafe instance starting with .NET 6
#else
        private static readonly object _randLock = new();
        private static readonly Random _rand = new();
#endif


        private readonly List<string> hostnames = new();
        private readonly Dictionary<string, DateTime> unavailable = new();
        private readonly object selectionLock = new();
        private string? baseUriFormat;
        private int hostnameIndex;

        internal IReadOnlyList<string> Hostnames => hostnames;

        /// <summary>
        /// Gets the operating-system resolver configuration when this endpoint represents system DNS.
        /// </summary>
        public SystemDnsConfiguration? SystemDnsConfiguration { get; private set; }

        /// <summary>
        /// Gets or sets whether unqualified names use operating-system search suffixes for system DNS endpoints.
        /// </summary>
        public bool UseSystemSearchDomains { get; set; } = true;

        /// <summary>
        /// Gets or sets whether system endpoints apply supported Windows NRPT resolver and DNSSEC policy.
        /// Matching policies that require unsupported Windows services fail explicitly when this is enabled.
        /// </summary>
        public bool UseSystemDnsPolicies { get; set; } = true;

        /// <summary>Gets the Windows NRPT match applied to this immutable query snapshot.</summary>
        public SystemDnsPolicyMatch? AppliedSystemDnsPolicy { get; private set; }

        /// <summary>
        /// Gets or sets the local network-interface index used for multicast DNS queries.
        /// A null value lets the operating system choose the interface.
        /// </summary>
        public int? MulticastInterfaceIndex { get; set; }

        /// <summary>
        /// Gets or sets the local endpoint used to bind UDP, TCP, DNS-over-TLS, and DNS-over-QUIC sockets.
        /// HTTP-based transports do not support this option and fail explicitly when it is set.
        /// Use port zero to let the operating system allocate an ephemeral source port.
        /// </summary>
        public IPEndPoint? LocalEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the cooldown period for hosts marked as unavailable.
        /// </summary>
        public TimeSpan UnavailableCooldown { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets how long resolved DNS server addresses stay fresh in the
        /// local cache.
        /// </summary>
        public TimeSpan DnsServerResolutionSuccessTtl { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets how long failed DNS server resolution results are cached.
        /// </summary>
        public TimeSpan DnsServerResolutionFailureTtl { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets how long a stale DNS server address may be reused after
        /// resolution failures.
        /// </summary>
        public TimeSpan DnsServerResolutionStaleTtl { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Gets or sets whether stale DNS server addresses can be reused when  
        /// resolution fails.
        /// </summary>
        public bool DnsServerResolutionAllowStale { get; set; } = true;

        /// <summary>
        /// Gets or sets whether repeated DNS server resolution failures should
        /// apply a backoff before retrying.
        /// </summary>
        public bool DnsServerResolutionFailureBackoffEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the backoff factor used to extend the failure TTL when
        /// resolution keeps failing.
        /// </summary>
        public double DnsServerResolutionFailureBackoffFactor { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the maximum TTL applied when failure backoff is enabled.
        /// </summary>
        public TimeSpan DnsServerResolutionFailureBackoffMaxTtl { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets how to select the DNS server to use when multiple are available.
        /// </summary>
        /// <value>
        /// The selection strategy.
        /// </value>
        public DnsSelectionStrategy SelectionStrategy { get; set; }

        /// <summary>
        /// Gets the hostname of the DNS-over-HTTPS resolver.
        /// </summary>
        public string? Hostname { get; set; }

        /// <summary>
        /// Gets the base URI to send DNS requests to.
        /// </summary>
        public Uri? BaseUri { get; set; }

        /// <summary>
        /// The preferred HTTP request version to use by default.
        /// </summary>
        public static readonly Version DefaultHttpVersion = new(2, 0);

        /// <summary>
        /// Default timeout for DNS queries in milliseconds.
        /// </summary>
        public const int DefaultTimeout = 2000;

        /// <summary>Default maximum number of referral and alias hops for iterative resolution.</summary>
        public const int DefaultIterativeMaxHops = 32;

        /// <summary>
        /// Default connection limit per server for HTTP requests.
        /// </summary>
        public const int DefaultMaxConnectionsPerServer = 10;

        /// <summary>
        /// Gets or sets the HTTP version used when communicating with the DNS provider.
        /// </summary>
        public Version HttpVersion { get; set; } = DefaultHttpVersion;

        /// <summary>
        /// Gets or sets the User-Agent header value to send along with DNS requests.
        /// </summary>
        public string UserAgent { get; set; } = "DnsClientX";

        /// <summary>
        /// Time-out for DNS Query in milliseconds. Valid only for UDP (for now).
        /// </summary>
        public int TimeOut = DefaultTimeout;

        /// <summary>
        /// Gets or sets the maximum referral and alias hops used by the <see cref="DnsEndpoint.RootServer"/> profile.
        /// This is independent of public retry-attempt settings.
        /// </summary>
        public int IterativeMaxHops { get; set; } = DefaultIterativeMaxHops;

        /// <summary>
        /// Gets or sets whether iterative root resolution uses RFC 9156 QNAME minimization while
        /// discovering delegation points. The complete name and requested type are sent only to
        /// the authoritative zone, except when a broken delegation requires an explicit fallback.
        /// </summary>
        public bool EnableQNameMinimization { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional path for persistent RFC 5011 root trust-anchor state.
        /// When null, validation uses only the immutable anchors bundled with this release.
        /// </summary>
        public string? Rfc5011TrustAnchorStorePath { get; set; }

        /// <summary>
        /// Sets the CD (Checking Disabled) flag on queries.
        /// </summary>
        public bool CheckingDisabled { get; set; }

        /// <summary>
        /// Gets or sets whether standard queries request recursive resolution. Disable this when
        /// querying authoritative servers directly or performing recursion diagnostics.
        /// </summary>
        public bool RecursionDesired { get; set; } = true;

        /// <summary>
        /// Gets or sets the TLS server name used for certificate validation and SNI on DoT and DoQ.
        /// This is required when a custom encrypted DNS endpoint is configured by IP address.
        /// </summary>
        public string? TlsServerName { get; set; }

        /// <summary>
        /// Gets or sets the preferred address family when a DNS server hostname resolves to both IPv4 and IPv6.
        /// The other family remains a fallback when the preferred family is unavailable.
        /// </summary>
        public AddressFamily? PreferredAddressFamily { get; set; }

        /// <summary>
        /// Determines whether to fall back to TCP when a UDP response is truncated.
        /// </summary>
        public bool UseTcpFallback { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of HTTP connections allowed per server.
        /// </summary>
        public int MaxConnectionsPerServer { get; set; } = DefaultMaxConnectionsPerServer;

        /// <summary>
        /// Gets or sets whether standard TCP and DoT queries reuse persistent RFC 7766 connections.
        /// Disable only for compatibility diagnostics with a server that cannot support connection reuse.
        /// </summary>
        public bool EnableTcpConnectionReuse { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of pipelined DNS queries awaiting responses on one TCP or DoT connection.
        /// </summary>
        public int MaxTcpQueriesPerConnection { get; set; } = 128;

        /// <summary>
        /// Optional cap for client-side query parallelism when resolving multiple names.
        /// When null, the library uses its current behavior (no explicit cap).
        /// When set to a positive value, array-based resolve helpers will limit
        /// the number of in-flight requests to this value.
        /// </summary>
        public int? MaxConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the TSIG key used to authenticate RFC 2136 DNS UPDATE requests and responses.
        /// </summary>
        public TsigKey? TsigKey { get; set; }

        /// <summary>
        /// Gets or sets the client certificate offered for mutual authentication during RFC 9103 XFR-over-TLS.
        /// </summary>
        public X509Certificate2? ZoneTransferClientCertificate { get; set; }

        /// <summary>
        /// Gets or sets an optional strict server-certificate validator for XFR-over-TLS, such as an SPKI pin.
        /// The platform trust validator is used when this is null.
        /// </summary>
        public RemoteCertificateValidationCallback? ZoneTransferServerCertificateValidationCallback { get; set; }

        /// <summary>Gets or sets the maximum number of DNS messages accepted in one zone transfer.</summary>
        public int MaxZoneTransferMessages { get; set; } = 100000;

        /// <summary>Gets or sets the maximum number of resource records accepted in one zone transfer.</summary>
        public int MaxZoneTransferRecords { get; set; } = 1000000;

        /// <summary>Gets or sets the maximum cumulative wire bytes accepted in one zone transfer.</summary>
        public long MaxZoneTransferBytes { get; set; } = 256L * 1024L * 1024L;

        /// <summary>
        /// Gets or sets the UDP buffer size used when sending EDNS queries.
        /// </summary>
        /// <value>The size of the UDP buffer.</value>
        public int UdpBufferSize { get; set; } = 4096;

        /// <summary>
        /// Gets or sets a value indicating whether EDNS should be enabled for queries.
        /// </summary>
        /// <value><c>true</c> to include the OPT record; otherwise, <c>false</c>.</value>
        public bool EnableEdns { get; set; }

        /// <summary>
        /// Gets or sets the EDNS Client Subnet (ECS) to include in DNS queries.
        /// Specify as CIDR notation, for example <c>192.0.2.0/24</c>.
        /// When configured, EDNS will be automatically enabled.
        /// </summary>
        public string? Subnet { get; set; }

        /// <summary>
        /// Gets or sets additional EDNS options. When configured, these values
        /// override <see cref="EnableEdns"/>, <see cref="UdpBufferSize"/> and
        /// <see cref="Subnet"/>.
        /// </summary>
        public EdnsOptions? EdnsOptions { get; set; }

        private DnsRequestFormat requestFormat;

        /// <summary>
        /// Gets or sets the format of the DNS requests.
        /// Updating the format adjusts <see cref="Port"/> according
        /// to the same rules used by the constructors.
        /// </summary>
        public DnsRequestFormat RequestFormat {
            get => requestFormat;
            set {
                requestFormat = value;
                if (value == DnsRequestFormat.DnsOverTLS || value == DnsRequestFormat.DnsOverQuic) {
                    Port = 853;
                } else if (value == DnsRequestFormat.DnsOverUDP || value == DnsRequestFormat.DnsOverTCP || value == DnsRequestFormat.DnsCryptRelay) {
                    Port = 53;
                } else if (value == DnsRequestFormat.Multicast) {
                    Port = 5353;
                } else {
                    Port = 443;
                }
            }
        }

        /// <summary>
        /// Gets or sets the port. The default value is 53 for DNS over UDP or DNS over TCP, and 853 for DNS over TLS.
        /// Only used when the request format is DNS over TLS, or DNS over UDP or TCP.
        /// In the case of DNS over HTTPS, the port is determined by the base URI.
        /// </summary>
        /// <value>
        /// The port.
        /// </value>
        public int Port { get; set; }

        /// <summary>
        /// Gets the built-in resolver profile used to create this configuration, when applicable.
        /// </summary>
        public DnsEndpoint? BuiltInEndpoint { get; }

        private void ApplyPortToBaseUri() {
            if ((RequestFormat == DnsRequestFormat.DnsOverTLS || RequestFormat == DnsRequestFormat.DnsOverQuic) && BaseUri != null && BaseUri.Port != Port) {
                var builder = new UriBuilder(BaseUri) { Port = Port };
                BaseUri = builder.Uri;
            }
        }

        private bool IsUnavailable(string host) {
            lock (unavailable) {
                if (unavailable.TryGetValue(host, out var until)) {
                    if (until > DateTime.UtcNow) {
                        return true;
                    }

                    unavailable.Remove(host);
                }

                return false;
            }
        }

        /// <summary>
        /// Marks the specified host as unavailable for the configured cooldown period.
        /// </summary>
        public void MarkHostnameUnavailable(string? host) {
            if (string.IsNullOrEmpty(host)) return;

            lock (unavailable) {
                unavailable[host!] = DateTime.UtcNow.Add(UnavailableCooldown);
            }
        }

        /// <summary>
        /// Marks the current hostname as unavailable.
        /// </summary>
        public void MarkCurrentHostnameUnavailable() {
            string? hostname;
            lock (selectionLock) {
                hostname = Hostname;
            }
            MarkHostnameUnavailable(hostname);
        }

        internal void AdvanceToNextHostname() {
            lock (selectionLock) {
                if (hostnames.Count <= 1) {
                    return;
                }

                hostnameIndex++;
                if (hostnameIndex >= hostnames.Count) {
                    hostnameIndex = 0;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific hostname and request format.
        /// </summary>
        /// <param name="hostname">The hostname of the DNS-over-HTTPS resolver.</param>
        /// <param name="requestFormat">The format of the DNS requests.</param>
        public Configuration(string hostname, DnsRequestFormat requestFormat) {
            if (string.IsNullOrWhiteSpace(hostname)) throw new ArgumentException("Hostname is null or whitespace.", nameof(hostname));
            hostnames = new List<string> { hostname };
            RequestFormat = requestFormat;
            if (requestFormat != DnsRequestFormat.DnsOverUDP && requestFormat != DnsRequestFormat.DnsOverTCP && requestFormat != DnsRequestFormat.DnsCryptRelay) {
                if (IPAddress.TryParse(hostname, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6) {
                    baseUriFormat = "https://[{0}]/dns-query";
                } else {
                    baseUriFormat = "https://{0}/dns-query";
                }
                BaseUri = new Uri(string.Format(baseUriFormat, hostname));
            } else {
                baseUriFormat = null;
                BaseUri = null;
            }
            hostnameIndex = 0;

            if (requestFormat == DnsRequestFormat.DnsOverTLS || requestFormat == DnsRequestFormat.DnsOverQuic) {
                Port = 853;
            } else if (requestFormat == DnsRequestFormat.DnsOverUDP || requestFormat == DnsRequestFormat.DnsOverTCP) {
                Port = 53;
            } else if (requestFormat == DnsRequestFormat.Multicast) {
                Port = 5353;
            } else {
                Port = 443;
            }
            ApplyPortToBaseUri();
        }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific base URI and request format.
        /// </summary>
        /// <param name="baseUri">The base URI to send DNS requests to.</param>
        /// <param name="requestFormat">The format of the DNS requests.</param>
        public Configuration(Uri baseUri, DnsRequestFormat requestFormat) {
            BaseUri = baseUri;
            RequestFormat = requestFormat;
            Hostname = baseUri.Host;
            hostnames = new List<string> { baseUri.Host };
            hostnameIndex = 0;

            if (requestFormat == DnsRequestFormat.DnsOverTLS || requestFormat == DnsRequestFormat.DnsOverQuic) {
                Port = 853;
            } else if (requestFormat == DnsRequestFormat.DnsOverUDP || requestFormat == DnsRequestFormat.DnsOverTCP) {
                Port = 53;
            } else {
                Port = 443;
            }
            ApplyPortToBaseUri();
        }

        /// <summary>
        /// Selects the Dns server based on the selection strategy.
        /// </summary>
        public void SelectHostNameStrategy() {
            lock (selectionLock) {
                SelectHostNameStrategyCore();
            }
        }

        /// <summary>
        /// Selects a resolver and freezes all query-visible endpoint values so concurrent
        /// calls cannot observe a hostname paired with another call's URI or port.
        /// </summary>
        internal Configuration CreateQuerySnapshot() {
            return CreateQuerySnapshot(null);
        }

        internal Configuration CreateQuerySnapshot(string? queryName) {
            lock (selectionLock) {
                SelectHostNameStrategyCore();
                Configuration snapshot = (Configuration)MemberwiseClone();
                snapshot.EdnsOptions = EdnsOptions?.Clone();
                snapshot.LocalEndPoint = LocalEndPoint == null
                    ? null
                    : new IPEndPoint(LocalEndPoint.Address, LocalEndPoint.Port);
                snapshot.ApplySystemDnsPolicy(queryName);
                return snapshot;
            }
        }

        private void ApplySystemDnsPolicy(string? queryName) {
            AppliedSystemDnsPolicy = null;
            if (!UseSystemDnsPolicies
                || string.IsNullOrWhiteSpace(queryName)
                || (BuiltInEndpoint != DnsEndpoint.System && BuiltInEndpoint != DnsEndpoint.SystemTcp)
                || SystemDnsConfiguration == null) {
                return;
            }

            SystemDnsPolicyMatch? match = SystemDnsConfiguration.MatchPolicy(queryName!);
            AppliedSystemDnsPolicy = match;
            if (match == null || !match.CanApply || match.NameServers.Count == 0) return;

            Hostname = SelectPolicyNameServer(match.NameServers);
            BaseUri = null;
            Port = 53;
        }

        private string SelectPolicyNameServer(IReadOnlyList<string> nameServers) {
            int selectedIndex;
            switch (SelectionStrategy) {
                case DnsSelectionStrategy.Random:
#if NET6_0_OR_GREATER
                    selectedIndex = Random.Shared.Next(nameServers.Count);
#else
                    lock (_randLock) selectedIndex = _rand.Next(nameServers.Count);
#endif
                    break;
                case DnsSelectionStrategy.Failover:
                    selectedIndex = hostnameIndex % nameServers.Count;
                    break;
                default:
                    selectedIndex = 0;
                    break;
            }

            for (int offset = 0; offset < nameServers.Count; offset++) {
                string candidate = nameServers[(selectedIndex + offset) % nameServers.Count];
                if (!IsUnavailable(candidate)) return candidate;
            }
            return nameServers[selectedIndex];
        }

        private void SelectHostNameStrategyCore() {
            if (hostnames.Count == 1) {
                Hostname = hostnames[0];
                if (baseUriFormat != null) {
                    BaseUri = new Uri(string.Format(baseUriFormat, Hostname));
                    ApplyPortToBaseUri();
                }
            } else if (hostnames.Count == 0) {
                // use BaseUri as is
            } else {
                // Select a hostname based on the selection strategy
                switch (SelectionStrategy) {
                    case DnsSelectionStrategy.First:
                        hostnameIndex = 0;
                        break;
                    case DnsSelectionStrategy.Random:
#if NET6_0_OR_GREATER
                        hostnameIndex = Random.Shared.Next(hostnames.Count);
#else
                        lock (_randLock) {
                            hostnameIndex = _rand.Next(hostnames.Count);
                        }
#endif
                        break;
                    case DnsSelectionStrategy.Failover:
                        if (hostnameIndex >= hostnames.Count) {
                            hostnameIndex = 0;
                        }
                        break;
                }

                var startIndex = hostnameIndex;
                for (var i = 0; i < hostnames.Count; i++) {
                    var candidate = hostnames[hostnameIndex];
                    if (!IsUnavailable(candidate)) {
                        Hostname = candidate;
                        break;
                    }

                    hostnameIndex++;
                    if (hostnameIndex >= hostnames.Count) {
                        hostnameIndex = 0;
                    }

                    if (hostnameIndex == startIndex) {
                        Hostname = candidate;
                        break;
                    }
                }

                if (baseUriFormat != null) {
                    BaseUri = new Uri(string.Format(baseUriFormat, Hostname));
                    ApplyPortToBaseUri();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific DNS endpoint.
        /// </summary>
        /// <param name="endpoint">The DNS endpoint to use.</param>
        /// <param name="selectionStrategy">DNS Selection Strategy</param>
        /// <param name="systemDnsFallback">Optional fallback used only when a system endpoint has no configured resolvers.</param>
        /// <exception cref="System.ArgumentException">Thrown when an invalid endpoint is provided.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a system endpoint has no configured resolver and no fallback was requested.</exception>
        public Configuration(
            DnsEndpoint endpoint,
            DnsSelectionStrategy selectionStrategy = DnsSelectionStrategy.First,
            SystemDnsFallback systemDnsFallback = SystemDnsFallback.None) {
            BuiltInEndpoint = endpoint;
            List<string> hostnames;
            SelectionStrategy = selectionStrategy;
            string? baseUriFormat;
            switch (endpoint) {
                case DnsEndpoint.System:
                    // Use the system's default DNS resolver
                    SystemDnsConfiguration = SystemInformation.GetDnsConfiguration(fallback: systemDnsFallback);
                    hostnames = new List<string>(SystemDnsConfiguration.DnsServers);
                    RequestFormat = DnsRequestFormat.DnsOverUDP;
                    baseUriFormat = null;
                    break;
                case DnsEndpoint.SystemTcp:
                    // Use the system's default DNS resolver
                    SystemDnsConfiguration = SystemInformation.GetDnsConfiguration(fallback: systemDnsFallback);
                    hostnames = new List<string>(SystemDnsConfiguration.DnsServers);
                    RequestFormat = DnsRequestFormat.DnsOverTCP;
                    baseUriFormat = null;
                    break;
                case DnsEndpoint.Cloudflare:
                    hostnames = ["1.1.1.1", "1.0.0.1"];
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareWireFormat:
                    hostnames = new List<string> { "1.1.1.1", "1.0.0.1" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareWireFormatPost:
                    hostnames = new List<string> { "1.1.1.1", "1.0.0.1" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsWirePost;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareJsonPost:
                    throw new NotSupportedException("Cloudflare does not publish a JSON-over-POST DNS endpoint. Use CloudflareWireFormatPost for RFC 8484 POST.");
                case DnsEndpoint.CloudflareSecurity:
                    hostnames = new List<string> { "1.1.1.2", "1.0.0.2" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareFamily:
                    hostnames = new List<string> { "1.1.1.3", "1.0.0.3" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareQuic:
                    throw new NotSupportedException("Cloudflare does not publish a DNS-over-QUIC resolver endpoint. Use Cloudflare DoH, DoH3, or DoT.");
                case DnsEndpoint.Quad9Http3:
                    hostnames = new List<string> { "dns.quad9.net" };
                    RequestFormat = DnsRequestFormat.DnsOverHttp3;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Quad9Quic:
                    hostnames = new List<string> { "dns.quad9.net" };
                    RequestFormat = DnsRequestFormat.DnsOverQuic;
                    TlsServerName = "dns.quad9.net";
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareOdoh:
                    throw new NotSupportedException("ODoH requires HPKE encapsulation and relay/target handling. It is intentionally not implemented by the core package.");
                case DnsEndpoint.Custom:
                    hostnames = [];
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = null;
                    break;
                case DnsEndpoint.Google:
                    hostnames = new List<string> { "dns.google" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.GoogleWireFormat:
                    hostnames = new List<string> { "dns.google" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.GoogleWireFormatPost:
                    hostnames = new List<string> { "dns.google" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsWirePost;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.GoogleJsonPost:
                    throw new NotSupportedException("Google Public DNS does not publish a JSON-over-POST endpoint. Use GoogleWireFormatPost for RFC 8484 POST.");
                case DnsEndpoint.GoogleQuic:
                    throw new NotSupportedException("Google Public DNS does not publish a DNS-over-QUIC resolver endpoint. Use Google DoH, DoH3, or DoT.");
                case DnsEndpoint.AdGuard:
                    hostnames = new List<string> { "dns.adguard.com" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.AdGuardFamily:
                    hostnames = new List<string> { "dns-family.adguard.com" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.AdGuardNonFiltering:
                    hostnames = new List<string> { "dns-unfiltered.adguard.com" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.NextDNS:
                    hostnames = new List<string> { "dns.nextdns.io" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Quad9:
                    hostnames = new List<string> { "dns.quad9.net" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Quad9ECS:
                    hostnames = new List<string> { "dns11.quad9.net" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Quad9Unsecure:
                    hostnames = new List<string> { "dns10.quad9.net" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.OpenDNS:
                    hostnames = new List<string> { "208.67.222.222", "208.67.220.220" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.OpenDNSFamily:
                    hostnames = ["208.67.222.123", "208.67.220.123"];
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.RootServer:
                    hostnames = new List<string>(RootServers.Servers);
                    RequestFormat = DnsRequestFormat.DnsOverUDP;
                    RecursionDesired = false;
                    baseUriFormat = null;
                    break;
                case DnsEndpoint.DnsCryptCloudflare:
                case DnsEndpoint.DnsCryptQuad9:
                case DnsEndpoint.DnsCryptRelay:
                    throw new NotSupportedException("DNSCrypt v2 is intentionally reserved for an optional protocol package; the core package does not implement its cryptography.");
                default:
                    throw new ArgumentException("Invalid endpoint", nameof(endpoint));
            }

            if (endpoint == DnsEndpoint.Custom && hostnames.Count == 0) {
                throw new ArgumentException("At least one hostname must be specified for a custom endpoint.", nameof(endpoint));
            }
            if ((endpoint == DnsEndpoint.System || endpoint == DnsEndpoint.SystemTcp) && hostnames.Count == 0) {
                throw new InvalidOperationException(
                    "No DNS servers were exposed by the operating system. Configure an explicit resolver or opt in to SystemDnsFallback.PublicResolvers.");
            }
            // Select a hostname based on the selection strategy
            this.hostnames = hostnames;
            this.baseUriFormat = baseUriFormat;
            hostnameIndex = 0;

            SelectHostNameStrategy();

            if (RequestFormat == DnsRequestFormat.DnsOverTLS ||
                RequestFormat == DnsRequestFormat.DnsOverQuic) {
                Port = 853;
            } else if (RequestFormat == DnsRequestFormat.DnsOverUDP ||
                       RequestFormat == DnsRequestFormat.DnsOverTCP ||
                       RequestFormat == DnsRequestFormat.DnsCryptRelay) {
                Port = 53;
            } else if (RequestFormat == DnsRequestFormat.Multicast) {
                Port = 5353;
            } else {
                Port = 443;
            }

            if (baseUriFormat != null && !string.IsNullOrEmpty(Hostname)) {
                BaseUri = new Uri(string.Format(baseUriFormat, Hostname));
                ApplyPortToBaseUri();
            }
        }
    }
}
