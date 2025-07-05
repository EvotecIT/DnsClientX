using System.Net;

namespace DnsClientX {
    /// <summary>
    /// Builder class for creating configured <see cref="ClientX"/> instances.
    /// </summary>
    public class ClientXBuilder {
        private DnsEndpoint _endpoint = DnsEndpoint.Cloudflare;
        private int _timeout = Configuration.DefaultTimeout;
        private IWebProxy? _proxy;

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
        /// Builds and returns a configured <see cref="ClientX"/> instance.
        /// </summary>
        public ClientX Build() {
            return new ClientX(_endpoint, DnsSelectionStrategy.First, _timeout, webProxy: _proxy);
        }
    }
}
