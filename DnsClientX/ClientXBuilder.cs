using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;

namespace DnsClientX {
    /// <summary>
    /// Builder class for creating configured <see cref="ClientX"/> instances.
    /// </summary>
    public class ClientXBuilder {
        private DnsEndpoint _endpoint = DnsEndpoint.Cloudflare;
        private int _timeout = Configuration.DefaultTimeout;
        private IWebProxy? _proxy;
        private EdnsOptions? _ednsOptions;
        private AsymmetricAlgorithm? _signingKey;
        private DnsSelectionStrategy _strategy = DnsSelectionStrategy.First;
        private string? _userAgent;
        private Version? _httpVersion;
        private bool _ignoreCertificateErrors;
        private bool _enableCache;
        private bool _useTcpFallback = true;
        private string? _hostname;
        private Uri? _baseUri;
        private DnsRequestFormat? _requestFormat;

        /// <summary>
        /// Sets the DNS endpoint to use.
        /// </summary>
        /// <param name="endpoint">Predefined DNS endpoint.</param>
        public ClientXBuilder WithEndpoint(DnsEndpoint endpoint) {
            if (endpoint == DnsEndpoint.Custom && _hostname == null && _baseUri == null) {
                throw new ArgumentException(
                    "EndpointConfiguration.Hostname or BaseUri must be set before selecting a custom endpoint.",
                    nameof(endpoint));
            }

            _endpoint = endpoint;
            return this;
        }

        /// <summary>
        /// Sets the DNS query timeout in milliseconds.
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds.</param>
        public ClientXBuilder WithTimeout(int timeout) {
            _timeout = timeout;
            return this;
        }

        /// <summary>
        /// Sets an optional web proxy for HTTP requests.
        /// </summary>
        /// <param name="proxy">The web proxy to use.</param>
        public ClientXBuilder WithProxy(IWebProxy proxy) {
            _proxy = proxy;
            return this;
        }

        /// <summary>
        /// Sets the DNS server selection strategy.
        /// </summary>
        public ClientXBuilder WithSelectionStrategy(DnsSelectionStrategy strategy) {
            _strategy = strategy;
            return this;
        }

        /// <summary>
        /// Sets the User-Agent header for HTTP requests.
        /// </summary>
        public ClientXBuilder WithUserAgent(string userAgent) {
            _userAgent = userAgent;
            return this;
        }

        /// <summary>
        /// Sets the preferred HTTP version used for queries.
        /// </summary>
        public ClientXBuilder WithHttpVersion(Version version) {
            _httpVersion = version;
            return this;
        }

        /// <summary>
        /// Allows ignoring certificate validation errors.
        /// </summary>
        public ClientXBuilder WithIgnoreCertificateErrors(bool ignore = true) {
            _ignoreCertificateErrors = ignore;
            return this;
        }

        /// <summary>
        /// Enables or disables response caching.
        /// </summary>
        public ClientXBuilder WithEnableCache(bool enable = true) {
            _enableCache = enable;
            return this;
        }

        /// <summary>
        /// Configures TCP fallback behavior for UDP queries.
        /// </summary>
        public ClientXBuilder WithUseTcpFallback(bool useTcpFallback) {
            _useTcpFallback = useTcpFallback;
            return this;
        }

        /// <summary>
        /// Configures a custom hostname and DNS request format.
        /// </summary>
        public ClientXBuilder WithHostname(string hostname, DnsRequestFormat format) {
            _hostname = hostname;
            _requestFormat = format;
            _endpoint = DnsEndpoint.Custom;
            return this;
        }

        /// <summary>
        /// Configures a custom base URI and DNS request format.
        /// </summary>
        public ClientXBuilder WithBaseUri(Uri baseUri, DnsRequestFormat format) {
            _baseUri = baseUri;
            _requestFormat = format;
            _endpoint = DnsEndpoint.Custom;
            return this;
        }

        /// <summary>
        /// Configures EDNS options for DNS queries.
        /// </summary>
        /// <param name="options">The EDNS options to apply.</param>
        public ClientXBuilder WithEdnsOptions(EdnsOptions options) {
            _ednsOptions = options;
            return this;
        }

        /// <summary>
        /// Supplies a key used to sign DNS messages.
        /// </summary>
        /// <param name="key">Asymmetric key pair.</param>
        public ClientXBuilder WithSigningKey(AsymmetricAlgorithm key) {
            _signingKey = key;
            return this;
        }

        /// <summary>
        /// Builds and returns a configured <see cref="ClientX"/> instance.
        /// </summary>
        public ClientX Build() {
            ClientX client;
            if (_baseUri != null && _requestFormat != null) {
                client = new ClientX(_baseUri, _requestFormat.Value, _timeout, _userAgent, _httpVersion, _ignoreCertificateErrors, _enableCache, _useTcpFallback, _proxy);
            } else if (_hostname != null && _requestFormat != null) {
                client = new ClientX(_hostname, _requestFormat.Value, _timeout, _userAgent, _httpVersion, _ignoreCertificateErrors, _enableCache, _useTcpFallback, _proxy);
            } else {
                client = new ClientX(_endpoint, _strategy, _timeout, _userAgent, _httpVersion, _ignoreCertificateErrors, _enableCache, _useTcpFallback, _proxy);
            }
            client.EndpointConfiguration.SelectHostNameStrategy();
            if (_ednsOptions != null) {
                client.EndpointConfiguration.EdnsOptions = _ednsOptions;
            }
            if (_signingKey != null) {
                client.EndpointConfiguration.SigningKey = _signingKey;
            }

            var field = typeof(Configuration).GetField("hostnames", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) {
                var names = (IEnumerable<string>)field.GetValue(client.EndpointConfiguration)!;
                foreach (var name in names) {
                    if (Uri.CheckHostName(name) == UriHostNameType.Unknown) {
                        throw new ArgumentException($"Invalid hostname: {name}");
                    }
                }
            } else if (client.EndpointConfiguration.Hostname != null &&
                       Uri.CheckHostName(client.EndpointConfiguration.Hostname) == UriHostNameType.Unknown) {
                throw new ArgumentException($"Invalid hostname: {client.EndpointConfiguration.Hostname}");
            }

            return client;
        }
    }
}