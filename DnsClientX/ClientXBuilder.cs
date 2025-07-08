using System;
using System.Net;
using System.Collections.Generic;
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

        /// <summary>
        /// Sets the DNS endpoint to use.
        /// </summary>
        /// <param name="endpoint">Predefined DNS endpoint.</param>
        public ClientXBuilder WithEndpoint(DnsEndpoint endpoint) {
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
            var client = new ClientX(_endpoint, DnsSelectionStrategy.First, _timeout, webProxy: _proxy);
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
