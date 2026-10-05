using System;
using System.Linq;
using System.Globalization;

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
            return new ClientX(CreateConfiguration(endpoint));
        }

        // One mapping owns endpoint behavior for single-target adapters and multi-resolver clients.
        internal static Configuration CreateConfiguration(DnsResolverEndpoint endpoint) {
            if (endpoint == null) {
                throw new ArgumentNullException(nameof(endpoint));
            }

            DnsRequestFormat requestFormat = endpoint.RequestFormat ?? DnsRequestFormatMapper.FromTransport(endpoint.Transport);
            if (requestFormat == DnsRequestFormat.ObliviousDnsOverHttps ||
                requestFormat == DnsRequestFormat.DnsCrypt ||
                requestFormat == DnsRequestFormat.DnsCryptRelay) {
                throw new NotSupportedException(DnsTransportCapabilities.GetUnsupportedMessage(requestFormat));
            }
            Configuration configuration;
            if (IsUriBasedRequestFormat(requestFormat) || endpoint.Transport == Transport.Doh || endpoint.DohUrl != null) {
                Uri dohUri = EndpointParser.BuildDohUri(endpoint);
                configuration = new Configuration(dohUri, requestFormat) { Port = dohUri.Port };
            } else {
                if (string.IsNullOrWhiteSpace(endpoint.Host)) {
                    throw new ArgumentException("Resolver endpoint requires Host.", nameof(endpoint));
                }
                configuration = new Configuration(endpoint.Host!, requestFormat) { Port = endpoint.Port };
            }
            configuration.TlsServerName = endpoint.TlsServerName;
            configuration.PreferredAddressFamily = endpoint.Family;
            configuration.UseTcpFallback = endpoint.AllowTcpFallback;
            if (endpoint.Timeout.HasValue) configuration.TimeOut = ToTimeoutMilliseconds(endpoint.Timeout.Value);
            if (endpoint.EdnsBufferSize.HasValue) configuration.UdpBufferSize = endpoint.EdnsBufferSize.Value;
            return configuration;
        }

        internal static int ToTimeoutMilliseconds(TimeSpan timeout) => timeout <= TimeSpan.Zero
            ? int.MaxValue : (int)Math.Min(int.MaxValue, Math.Max(1, timeout.TotalMilliseconds));

        // Preserve path/query case and distinguish every endpoint execution/security option.
        // Display labels are deliberately shorter and cannot serve as execution identities.
        internal static string GetExecutionKey(DnsResolverEndpoint endpoint) {
            DnsRequestFormat format = endpoint.RequestFormat ?? DnsRequestFormatMapper.FromTransport(endpoint.Transport);
            string address = IsUriBasedRequestFormat(format) || endpoint.Transport == Transport.Doh || endpoint.DohUrl != null
                ? EndpointParser.BuildDohUri(endpoint).AbsoluteUri
                : (endpoint.Host ?? string.Empty).TrimEnd('.').ToLowerInvariant();
            return string.Join("|", format.ToString(), address, endpoint.Port.ToString(CultureInfo.InvariantCulture),
                endpoint.Family?.ToString() ?? string.Empty, endpoint.TlsServerName?.ToLowerInvariant() ?? string.Empty,
                endpoint.AllowTcpFallback.ToString(), endpoint.EdnsBufferSize?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                endpoint.DnsSecOk?.ToString() ?? string.Empty, endpoint.Timeout?.Ticks.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
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
