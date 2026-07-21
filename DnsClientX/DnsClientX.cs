using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Threading;

namespace DnsClientX {
    /// <summary>
    /// The primary class for sending DNS over HTTPS queries.
    /// </summary>
    /// <remarks>
    /// All high level query methods are implemented as partial members across multiple files.
    /// </remarks>
    public partial class ClientX {
        /// <summary>
        /// The client
        /// </summary>
        private HttpClient? Client;

        /// <summary>
        /// Gets the endpoint configuration.
        /// </summary>
        /// <value>
        /// The endpoint configuration.
        /// </value>
        public Configuration EndpointConfiguration { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ClientX"/> is debug.
        /// </summary>
        /// <value>
        ///   <c>true</c> if debug; otherwise, <c>false</c>.
        /// </value>
        public bool Debug { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [ignore certificate errors].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [ignore certificate errors]; otherwise, <c>false</c>.
        /// </value>
        public bool IgnoreCertificateErrors { get; set; }

        /// <summary>
        /// Gets or sets the security protocol.
        /// </summary>
        /// <value>
        /// The security protocol.
        /// </value>
#if NET472 || NETSTANDARD2_0
        private SecurityProtocolType _securityProtocol = SecurityProtocolType.Tls12;
#else
        private SecurityProtocolType _securityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
#endif

        /// <summary>
        /// The handler
        /// </summary>
        private HttpClientHandler? handler;
        private bool _handlerOwnedByClient;

        /// <summary>
        /// Optional proxy used for HTTP requests
        /// </summary>
        private readonly IWebProxy? _webProxy;

        /// <summary>
        /// Collection storing audit trail entries when <see cref="EnableAudit"/> is set.
        /// </summary>
        private readonly ConcurrentQueue<AuditEntry> _auditTrail = new();
        private readonly AsyncLocal<AuditEntry?> _lastAuditEntryContext = new();
        private int _auditAttemptCounter;

        /// <summary>
        /// The lock for thread safety
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// Dictionary of clients for different selection strategies
        /// </summary>
        private readonly Dictionary<DnsSelectionStrategy, HttpClient> _clients = new Dictionary<DnsSelectionStrategy, HttpClient>();
        private readonly HashSet<HttpClient> _managedClients = new HashSet<HttpClient>();

        private static readonly DnsResponseCache _cache = new();
        private readonly bool _cacheEnabled;
        private readonly DnsUdpClientPool _udpClientPool = new();
#if NET8_0_OR_GREATER
        private readonly DnsQuicConnectionPool _quicConnectionPool = new();
#endif

        /// <summary>
        /// Gets a value indicating whether caching is enabled.
        /// </summary>
        public bool CacheEnabled => _cacheEnabled;

        /// <summary>
        /// Gets or sets the maximal TTL allowed for cached responses.
        /// </summary>
        public TimeSpan MaxCacheTtl { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets a value indicating whether audit logging is enabled.
        /// </summary>
        public bool EnableAudit { get; set; }

        /// <summary>
        /// Gets the audit trail entries recorded during the lifetime of the client.
        /// </summary>
        public IReadOnlyCollection<AuditEntry> AuditTrail => _auditTrail.ToArray();

        /// <summary>
        /// Gets or sets the security protocol. The default value is <see cref="SecurityProtocolType.Tls12"/> which is required by Quad 9.
        /// </summary>
        /// <value>
        /// The security protocol.
        /// </value>
        public SecurityProtocolType SecurityProtocol {
            get => _securityProtocol;
            set {
                _securityProtocol = value;
                if (handler != null) {
                    handler.SslProtocols = (SslProtocols)value;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientX"/> class.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        /// <param name="dnsSelectionStrategy">DNS selection strategy.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="userAgent">Optional User-Agent header value.</param>
        /// <param name="httpVersion">Optional HTTP protocol version.</param>
        /// <param name="ignoreCertificateErrors">Ignore certificate validation errors.</param>
        /// <param name="enableCache">Enable in-memory caching of responses.</param>
        /// <param name="useTcpFallback">Falls back to TCP when UDP responses are truncated.</param>
        /// <param name="webProxy">Optional HTTP proxy.</param>
        /// <param name="maxConnectionsPerServer">Maximum number of concurrent connections per server.</param>
        /// <param name="systemDnsFallback">Optional fallback used only when a system endpoint has no configured resolvers.</param>
        public ClientX(
            DnsEndpoint endpoint = DnsEndpoint.Cloudflare,
            DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First,
            int timeOutMilliseconds = Configuration.DefaultTimeout,
            string? userAgent = null,
            Version? httpVersion = null,
            bool ignoreCertificateErrors = false,
            bool enableCache = false,
            bool useTcpFallback = true,
            IWebProxy? webProxy = null,
            int maxConnectionsPerServer = Configuration.DefaultMaxConnectionsPerServer,
            SystemDnsFallback systemDnsFallback = SystemDnsFallback.None) {
            EndpointConfiguration = new Configuration(endpoint, dnsSelectionStrategy, systemDnsFallback) {
                TimeOut = timeOutMilliseconds,
                MaxConnectionsPerServer = maxConnectionsPerServer
            };
            if (userAgent != null) {
                EndpointConfiguration.UserAgent = userAgent;
            }
            if (httpVersion != null) {
                EndpointConfiguration.HttpVersion = httpVersion;
            }
            EndpointConfiguration.UseTcpFallback = useTcpFallback;
            IgnoreCertificateErrors = ignoreCertificateErrors;
            _cacheEnabled = enableCache;
            _webProxy = webProxy;
            ConfigureClient();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientX"/> class.
        /// </summary>
        /// <param name="hostname">The hostname.</param>
        /// <param name="requestFormat">The request format.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="userAgent">Optional User-Agent header value.</param>
        /// <param name="httpVersion">Optional HTTP protocol version.</param>
        /// <param name="ignoreCertificateErrors">Ignore certificate validation errors.</param>
        /// <param name="enableCache">Enable in-memory caching of responses.</param>
        /// <param name="useTcpFallback">Falls back to TCP when UDP responses are truncated.</param>
        /// <param name="webProxy">Optional HTTP proxy.</param>
        /// <param name="maxConnectionsPerServer">Maximum number of concurrent connections per server.</param>
        public ClientX(
            string hostname,
            DnsRequestFormat requestFormat,
            int timeOutMilliseconds = Configuration.DefaultTimeout,
            string? userAgent = null,
            Version? httpVersion = null,
            bool ignoreCertificateErrors = false,
            bool enableCache = false,
            bool useTcpFallback = true,
            IWebProxy? webProxy = null,
            int maxConnectionsPerServer = Configuration.DefaultMaxConnectionsPerServer) {
            EndpointConfiguration = new Configuration(hostname, requestFormat) {
                TimeOut = timeOutMilliseconds,
                MaxConnectionsPerServer = maxConnectionsPerServer
            };
            if (userAgent != null) {
                EndpointConfiguration.UserAgent = userAgent;
            }
            if (httpVersion != null) {
                EndpointConfiguration.HttpVersion = httpVersion;
            }
            EndpointConfiguration.UseTcpFallback = useTcpFallback;
            IgnoreCertificateErrors = ignoreCertificateErrors;
            _cacheEnabled = enableCache;
            _webProxy = webProxy;
            ConfigureClient();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientX"/> class.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <param name="requestFormat">The request format.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="userAgent">Optional User-Agent header value.</param>
        /// <param name="httpVersion">Optional HTTP protocol version.</param>
        /// <param name="ignoreCertificateErrors">Ignore certificate validation errors.</param>
        /// <param name="enableCache">Enable in-memory caching of responses.</param>
        /// <param name="useTcpFallback">Falls back to TCP when UDP responses are truncated.</param>
        /// <param name="webProxy">Optional HTTP proxy.</param>
        /// <param name="maxConnectionsPerServer">Maximum number of concurrent connections per server.</param>
        public ClientX(
            Uri baseUri,
            DnsRequestFormat requestFormat,
            int timeOutMilliseconds = Configuration.DefaultTimeout,
            string? userAgent = null,
            Version? httpVersion = null,
            bool ignoreCertificateErrors = false,
            bool enableCache = false,
            bool useTcpFallback = true,
            IWebProxy? webProxy = null,
            int maxConnectionsPerServer = Configuration.DefaultMaxConnectionsPerServer) {
            EndpointConfiguration = new Configuration(baseUri, requestFormat) {
                TimeOut = timeOutMilliseconds,
                MaxConnectionsPerServer = maxConnectionsPerServer
            };
            if (userAgent != null) {
                EndpointConfiguration.UserAgent = userAgent;
            }
            if (httpVersion != null) {
                EndpointConfiguration.HttpVersion = httpVersion;
            }
            EndpointConfiguration.UseTcpFallback = useTcpFallback;
            IgnoreCertificateErrors = ignoreCertificateErrors;
            _cacheEnabled = enableCache;
            _webProxy = webProxy;
            ConfigureClient();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientX"/> class using a preconfigured endpoint configuration.
        /// The configuration instance is used directly and the hostname selection strategy is applied during construction.
        /// </summary>
        /// <param name="configuration">The endpoint configuration to use.</param>
        /// <param name="ignoreCertificateErrors">Ignore certificate validation errors.</param>
        /// <param name="enableCache">Enable in-memory caching of responses.</param>
        /// <param name="webProxy">Optional HTTP proxy.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
        public ClientX(
            Configuration configuration,
            bool ignoreCertificateErrors = false,
            bool enableCache = false,
            IWebProxy? webProxy = null) {
            EndpointConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            EndpointConfiguration.SelectHostNameStrategy();
            IgnoreCertificateErrors = ignoreCertificateErrors;
            _cacheEnabled = enableCache;
            _webProxy = webProxy;
            ConfigureClient();
        }

        /// <summary>
        /// Creates an optimized HttpClient with proper connection management and realistic timeouts
        /// </summary>
        private HttpClient CreateOptimizedHttpClient(Configuration configuration) {
            // Configure TLS protocols
#if NET472 || NETSTANDARD2_0
            _securityProtocol = SecurityProtocolType.Tls12;
#else
            _securityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
#endif

            // Create handler with proper connection management
            handler = new HttpClientHandler();
            if (IgnoreCertificateErrors) {
                handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            }

            handler.SslProtocols = (SslProtocols)SecurityProtocol;
            if (_webProxy != null) {
                handler.Proxy = _webProxy;
                handler.UseProxy = true;
            } else {
                handler.UseProxy = false;
            }

            // Optimize connection settings for DNS workloads
            handler.MaxConnectionsPerServer = configuration.MaxConnectionsPerServer;
            handler.UseCookies = false; // DNS doesn't need cookies

            var client = new HttpClient(handler) {
                BaseAddress = configuration.BaseUri,
                Timeout = TimeSpan.FromMilliseconds(configuration.TimeOut)
            };

            _handlerOwnedByClient = true;

#if NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER
            client.DefaultRequestVersion = configuration.HttpVersion;
#endif
#if NET5_0_OR_GREATER
            // RFC 8484 clients should use HTTP/2 when available. Do not silently downgrade the
            // default HTTP/2 request to HTTP/1.1, which some conforming resolvers reject.
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
#endif
            // Set the user agent to the default value
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(configuration.UserAgent);
            client.DefaultRequestHeaders.Accept.Clear();

            // Set the accept header based on the request format, which is required for proper processing
            if (configuration.RequestFormat == DnsRequestFormat.DnsOverHttps ||
                configuration.RequestFormat == DnsRequestFormat.DnsOverHttpsPOST ||
                configuration.RequestFormat == DnsRequestFormat.DnsOverHttpsWirePost ||
                configuration.RequestFormat == DnsRequestFormat.DnsOverHttp2 ||
#if NET8_0_OR_GREATER
                configuration.RequestFormat == DnsRequestFormat.DnsOverHttp3 ||
#endif
                configuration.RequestFormat == DnsRequestFormat.ObliviousDnsOverHttps) {
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/dns-message"));
            } else {
                client.DefaultRequestHeaders.Accept.ParseAdd("application/dns-json");
            }

            return client;
        }

        /// <summary>
        /// Configures the client to required parameters (legacy method for backward compatibility)
        /// </summary>
        private void ConfigureClient() {
            lock (_lock) {
                if (Client != null && TryAddDisposedClient(Client)) {
                    _managedClients.Remove(Client);
                    Client.Dispose();
                    if (_handlerOwnedByClient && handler != null) {
                        TryAddDisposedClient(handler);
                    }
                }
                if (!_handlerOwnedByClient && handler != null && TryAddDisposedClient(handler)) {
                    handler.Dispose();
                }

                Client = CreateOptimizedHttpClient(EndpointConfiguration);
                _managedClients.Add(Client);
                _clients[EndpointConfiguration.SelectionStrategy] = Client;
            }
        }

        /// <summary>
        /// Gets the client based on the selection strategy
        /// This allows us to have multiple clients for different strategies, so performance is not affected
        /// </summary>
        /// <param name="configuration">Immutable configuration snapshot for the query.</param>
        /// <returns></returns>
        private HttpClient GetClient(Configuration configuration) {
            DnsSelectionStrategy strategy = configuration.SelectionStrategy;
            lock (_lock) {
                if (_clients.TryGetValue(strategy, out HttpClient? client) && client != null) {
                    if (!ClientMatchesConfiguration(client, configuration)) {
                        RecreateClientForStrategy(strategy, configuration);
                        client = _clients[strategy];
                    }
                    Client = client;
                    return client;
                }

                HttpClient created = CreateOptimizedHttpClient(configuration);
                _clients[strategy] = created;
                _managedClients.Add(created);
                Client = created;
                return created;
            }
        }

        private bool ClientMatchesConfiguration(HttpClient client, Configuration configuration) {
            if (!_managedClients.Contains(client)) {
                return true;
            }

            // BaseAddress is deliberately excluded: every request uses the absolute URI captured in
            // its query snapshot, so one pooled client can safely retain connections for several hosts.
            TimeSpan expectedTimeout = TimeSpan.FromMilliseconds(configuration.TimeOut);
            return client.Timeout == expectedTimeout;
        }

        private void RecreateClientForStrategy(DnsSelectionStrategy strategy, Configuration configuration) {
            // Do not dispose the previous client here: another query may still be using it. All managed
            // clients are retained until ClientX is disposed, while the strategy map stays enum-bounded.
            var replacement = CreateOptimizedHttpClient(configuration);
            _managedClients.Add(replacement);
            _clients[strategy] = replacement;
            Client = replacement;
        }

        /// <summary>
        /// Converts a domain name to its Punycode representation. This is useful for internationalized domain names (IDNs).
        /// For example www.bücher.de will be converted to www.xn--bcher-kva.de
        /// </summary>
        /// <param name="domainName"></param>
        /// <returns></returns>
        private static string ConvertToPunycode(string domainName) {
            if (string.IsNullOrWhiteSpace(domainName)) {
                return domainName;
            }

            bool hasTrailingDot = domainName.EndsWith(".", StringComparison.Ordinal);
            string nameToConvert = hasTrailingDot
                ? domainName.Substring(0, domainName.Length - 1)
                : domainName;

            foreach (char c in nameToConvert) {
                UnicodeCategory cat = char.GetUnicodeCategory(c);
                if (cat is UnicodeCategory.OtherSymbol
                    or UnicodeCategory.PrivateUse
                    or UnicodeCategory.Surrogate) {
                    return domainName;
                }
            }

            IdnMapping idn = new IdnMapping();
            try {
                string converted = idn.GetAscii(nameToConvert);
                return hasTrailingDot ? converted + "." : converted;
            } catch {
                return domainName;
            }
        }

        /// <summary>
        /// Converts an IP address to its PTR format. This is useful for reverse DNS lookups.
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        private string ConvertToPtrFormat(string ipAddress) {
            ipAddress = ipAddress.Trim();
            if (IPAddress.TryParse(ipAddress, out IPAddress? ip)) {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                    // IPv4
                    var bytes = ip.GetAddressBytes();
                    return string.Join(".", Enumerable.Reverse(bytes).Select(b => b.ToString())) + ".in-addr.arpa";
                } else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
                    // IPv6 – reverse each hex nibble (RFC 3596)
                    var bytes = ip.GetAddressBytes();
                    return string.Join(".", bytes
                        .SelectMany(b => b.ToString("x2"))
                        .Reverse()
                        .Select(c => c.ToString())) + ".ip6.arpa";
                }
            }
            // Invalid IP address, we return as is
            return ipAddress;
        }
    }
}
