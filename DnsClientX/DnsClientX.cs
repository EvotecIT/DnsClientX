using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;

namespace DnsClientX {
    /// <summary>
    /// The primary class for sending DNS over HTTPS queries.
    /// </summary>
    public partial class ClientX : IDisposable {
        /// <summary>
        /// The client
        /// </summary>
        private HttpClient Client;

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
        private SecurityProtocolType _securityProtocol = SecurityProtocolType.Tls12;

        /// <summary>
        /// The handler
        /// </summary>
        private HttpClientHandler handler;

        /// <summary>
        /// The lock for thread safety
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// Dictionary of clients for different selection strategies
        /// </summary>
        private readonly Dictionary<DnsSelectionStrategy, HttpClient> _clients = new Dictionary<DnsSelectionStrategy, HttpClient>();

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
#if NET472
                ServicePointManager.SecurityProtocol = value;
#endif
                if (handler != null) {
                    handler.SslProtocols = (SslProtocols)value;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientX"/> class.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        /// <param name="dnsSelectionStrategy">Dns selection strategy</param>
        /// <param name="timeOutMilliseconds"></param>
        public ClientX(DnsEndpoint endpoint = DnsEndpoint.Cloudflare, DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First, int timeOutMilliseconds = 1000) {
            EndpointConfiguration = new Configuration(endpoint, dnsSelectionStrategy) {
                TimeOut = timeOutMilliseconds
            };
            ConfigureClient();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientX"/> class.
        /// </summary>
        /// <param name="hostname">The hostname.</param>
        /// <param name="requestFormat">The request format.</param>
        /// <param name="timeOutMilliseconds"></param>
        public ClientX(string hostname, DnsRequestFormat requestFormat, int timeOutMilliseconds = 1000) {
            EndpointConfiguration = new Configuration(hostname, requestFormat) {
                TimeOut = timeOutMilliseconds
            };
            ConfigureClient();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientX"/> class.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <param name="requestFormat">The request format.</param>
        /// <param name="timeOutMilliseconds"></param>
        public ClientX(Uri baseUri, DnsRequestFormat requestFormat, int timeOutMilliseconds = 1000) {
            EndpointConfiguration = new Configuration(baseUri, requestFormat) {
                TimeOut = timeOutMilliseconds
            };
            ConfigureClient();
        }

        /// <summary>
        /// Configures the client to required parameters
        /// </summary>
        private void ConfigureClient() {
            lock (_lock) {
                // let's allow TLS 1.2 by default as Quad9 requires it
                SecurityProtocol = SecurityProtocolType.Tls12;

                Client?.Dispose();
                handler?.Dispose();

                // let's allow self-signed certificates if we want to
                handler = new HttpClientHandler();
                if (IgnoreCertificateErrors) {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                }

                handler.SslProtocols = (SslProtocols)SecurityProtocol;

                Client = new HttpClient(handler) {
                    BaseAddress = EndpointConfiguration.BaseUri,
                };

#if NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER
                Client.DefaultRequestVersion = Configuration.HttpVersion;
#endif
                // Set the user agent to the default value
                Client.DefaultRequestHeaders.UserAgent.ParseAdd(EndpointConfiguration.UserAgent);
                Client.DefaultRequestHeaders.Accept.Clear();

                // Set the accept header based on the request format, which is required for proper processing
                if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttps ||
                    EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsPOST) {
                    Client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/dns-message"));
                } else {
                    Client.DefaultRequestHeaders.Accept.ParseAdd("application/dns-json");
                }
            }
        }

        /// <summary>
        /// Gets the client based on the selection strategy
        /// This allows us to have multiple clients for different strategies, so performance is not affected
        /// </summary>
        /// <param name="strategy">The strategy.</param>
        /// <returns></returns>
        private HttpClient GetClient(DnsSelectionStrategy strategy) {
            if (!_clients.TryGetValue(strategy, out var client)) {
                lock (_lock) {
                    if (!_clients.TryGetValue(strategy, out client)) {
                        ConfigureClient();
                        client = Client;
                        _clients[strategy] = client;
                    }
                }
            }
            return client;
        }

        /// <summary>
        /// Converts a domain name to its Punycode representation. This is useful for internationalized domain names (IDNs).
        /// For example www.bücher.de will be converted to www.xn--bcher-kva.de
        /// </summary>
        /// <param name="domainName"></param>
        /// <returns></returns>
        private static string ConvertToPunycode(string domainName) {
            IdnMapping idn = new IdnMapping();
            return idn.GetAscii(domainName);
        }

        /// <summary>
        /// Converts an IP address to its PTR format. This is useful for reverse DNS lookups.
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        private string ConvertToPtrFormat(string ipAddress) {
            if (IPAddress.TryParse(ipAddress, out IPAddress? ip)) {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                    // IPv4
                    return string.Join(".", ip.GetAddressBytes().Reverse()) + ".in-addr.arpa";
                } else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
                    // IPv6
                    return string.Join(".", ip.GetAddressBytes()
                        .SelectMany(b => b.ToString("x2"))
                        .Reverse()) + ".ip6.arpa";
                }
            }
            // Invalid IP address, we return as is
            return ipAddress;
        }
    }
}
