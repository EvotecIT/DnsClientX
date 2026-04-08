using System;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Creates <see cref="ClientX"/> instances from explicit resolver endpoint descriptions.
    /// </summary>
    public static class ResolverEndpointClientFactory {
        /// <summary>
        /// Creates a client for the supplied resolver endpoint.
        /// </summary>
        /// <param name="endpoint">The explicit resolver endpoint definition.</param>
        /// <returns>A configured client instance for the supplied endpoint.</returns>
        public static ClientX CreateClient(DnsResolverEndpoint endpoint) {
            if (endpoint == null) {
                throw new ArgumentNullException(nameof(endpoint));
            }

            DnsRequestFormat requestFormat = endpoint.RequestFormat ?? DnsRequestFormatMapper.FromTransport(endpoint.Transport);
            if (IsUriBasedRequestFormat(requestFormat) || endpoint.Transport == Transport.Doh || endpoint.DohUrl != null) {
                Uri dohUri = EndpointParser.BuildDohUri(endpoint);
                return new ClientX(dohUri, requestFormat);
            }

            if (string.IsNullOrWhiteSpace(endpoint.Host)) {
                throw new ArgumentException("Resolver endpoint requires Host.", nameof(endpoint));
            }

            var client = new ClientX(endpoint.Host!, requestFormat);
            client.EndpointConfiguration.Port = endpoint.Port;
            return client;
        }

        /// <summary>
        /// Describes the configured resolver endpoint of an existing client instance.
        /// </summary>
        /// <param name="client">The client to describe.</param>
        /// <returns>A host-and-port description of the effective resolver endpoint.</returns>
        public static string DescribeConfiguredResolver(ClientX client) {
            if (client == null) {
                throw new ArgumentNullException(nameof(client));
            }

            string host = client.EndpointConfiguration.BaseUri?.Host
                ?? client.EndpointConfiguration.Hostname
                ?? client.EndpointConfiguration.Hostnames.FirstOrDefault()
                ?? "(unknown)";
            int port = client.EndpointConfiguration.BaseUri?.Port ?? client.EndpointConfiguration.Port;
            return $"{host}:{port}";
        }

        internal static bool IsUriBasedRequestFormat(DnsRequestFormat requestFormat) {
            return requestFormat == DnsRequestFormat.DnsOverHttps ||
                   requestFormat == DnsRequestFormat.DnsOverHttpsJSON ||
                   requestFormat == DnsRequestFormat.DnsOverHttpsPOST ||
                   requestFormat == DnsRequestFormat.DnsOverHttpsWirePost ||
                   requestFormat == DnsRequestFormat.DnsOverHttpsJSONPOST ||
                   requestFormat == DnsRequestFormat.DnsOverHttp2 ||
                   requestFormat == DnsRequestFormat.DnsOverHttp3 ||
                   requestFormat == DnsRequestFormat.ObliviousDnsOverHttps;
        }
    }
}
