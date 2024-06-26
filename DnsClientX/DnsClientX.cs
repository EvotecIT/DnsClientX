using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;

namespace DnsClientX {
    /// <summary>
    /// The primary class for sending DNS over HTTPS queries.
    /// </summary>
    public partial class ClientX {
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
                ServicePointManager.SecurityProtocol = value;
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
        public ClientX(DnsEndpoint endpoint = DnsEndpoint.Cloudflare, DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First) {
            EndpointConfiguration = new Configuration(endpoint, dnsSelectionStrategy);
            ConfigureClient();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientX"/> class.
        /// </summary>
        /// <param name="hostname">The hostname.</param>
        /// <param name="requestFormat">The request format.</param>
        public ClientX(string hostname, DnsRequestFormat requestFormat) {
            EndpointConfiguration = new Configuration(hostname, requestFormat);
            ConfigureClient();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientX"/> class.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <param name="requestFormat">The request format.</param>
        public ClientX(Uri baseUri, DnsRequestFormat requestFormat) {
            EndpointConfiguration = new Configuration(baseUri, requestFormat);
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
    }
}
