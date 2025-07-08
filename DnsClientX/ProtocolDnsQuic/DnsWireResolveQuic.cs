using System;
using System.Net;
#if NET8_0_OR_GREATER
using System.Net.Quic;
#endif
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
#if NET8_0_OR_GREATER
    #pragma warning disable CA2252
    /// <summary>
    /// Helper methods for resolving DNS queries over QUIC transport.
    /// </summary>
    internal static class DnsWireResolveQuic {
        /// <summary>Custom DNS host name resolution delegate used during tests.</summary>
        internal static Func<string, IPHostEntry>? HostEntryResolver;
        /// <summary>Factory function for creating QUIC connections. Can be overridden in tests.</summary>
        internal static Func<QuicClientConnectionOptions, CancellationToken, ValueTask<QuicConnection>> QuicConnectionFactory { get; set; } = QuicConnection.ConnectAsync;
        /// <summary>Delegate for creating outbound streams. Overridable for tests.</summary>
        internal static Func<QuicConnection, CancellationToken, ValueTask<QuicStream>> StreamFactory { get; set; } = static (c, t) => c.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, t);

        /// <summary>Delegate for disposing <see cref="QuicConnection"/> instances. Overridable for tests.</summary>
        internal static Func<QuicConnection, ValueTask> ConnectionDisposer { get; set; } = static c => c.DisposeAsync();
        /// <summary>Delegate for disposing <see cref="QuicStream"/> instances. Overridable for tests.</summary>
        internal static Func<QuicStream, ValueTask> StreamDisposer { get; set; } = static s => s.DisposeAsync();

        /// <summary>Counts how often connections were disposed.</summary>
        internal static int ConnectionDisposeCount;
        /// <summary>Counts how often streams were disposed.</summary>
        internal static int StreamDisposeCount;
        /// <summary>
        /// Executes a DNS-over-QUIC query and returns the parsed response.
        /// </summary>
        /// <param name="dnsServer">Target DNS server.</param>
        /// <param name="port">QUIC port to use.</param>
        /// <param name="name">Domain name to query.</param>
        /// <param name="type">Record type to query.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="debug">Enable detailed logging.</param>
        /// <param name="endpointConfiguration">Endpoint configuration details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Parsed <see cref="DnsResponse"/> from the server.</returns>
        internal static async Task<DnsResponse> ResolveWireFormatQuic(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var edns = endpointConfiguration.EdnsOptions;
            bool enableEdns = edns?.EnableEdns ?? endpointConfiguration.EnableEdns;
            int udpSize = edns?.UdpBufferSize ?? endpointConfiguration.UdpBufferSize;
            string? subnet = edns?.Subnet ?? endpointConfiguration.Subnet;
            var query = new DnsMessage(name, type, requestDnsSec, enableEdns, udpSize, subnet, endpointConfiguration.CheckingDisabled, endpointConfiguration.SigningKey);
            var queryBytes = query.SerializeDnsWireFormat();

            var lengthPrefix = BitConverter.GetBytes((ushort)queryBytes.Length);
            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lengthPrefix);
            }

            var payload = new byte[lengthPrefix.Length + queryBytes.Length];
            Buffer.BlockCopy(lengthPrefix, 0, payload, 0, lengthPrefix.Length);
            Buffer.BlockCopy(queryBytes, 0, payload, lengthPrefix.Length, queryBytes.Length);

            if (debug) {
                Settings.Logger.WriteDebug($"Query Name: {name} type: {type}");
                Settings.Logger.WriteDebug($"Sending combined query: {BitConverter.ToString(payload)}");
            }

            // Normalize the DNS server address. If it's not an IP address,
            // resolve it using DNS. IPv6 addresses should be wrapped in
            // brackets when constructing the endpoint string.
            IPAddress ipAddress;
            if (!IPAddress.TryParse(dnsServer, out ipAddress)) {
                var hostEntry = HostEntryResolver?.Invoke(dnsServer) ?? Dns.GetHostEntry(dnsServer);
                if (hostEntry.AddressList.Length == 0) {
                    var failureResponse = new DnsResponse {
                        Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverQuic, Type = type, OriginalName = name } ],
                        Status = DnsResponseCode.ServerFailure
                    };
                    failureResponse.AddServerDetails(endpointConfiguration);
                    failureResponse.Error = $"Host '{dnsServer}' resolved to no addresses.";
                    return failureResponse;
                }
                ipAddress = hostEntry.AddressList[0];
            }

            var ipString = ipAddress.ToString();
            int zoneIndex = ipString.IndexOf('%');
            if (zoneIndex >= 0) {
                ipString = ipString[..zoneIndex];
            }

            if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6) {
                ipString = $"[{ipString}]";
            }

            var endPoint = IPEndPoint.Parse($"{ipString}:{port}");
            var options = new QuicClientConnectionOptions {
                RemoteEndPoint = endPoint,
                ClientAuthenticationOptions = new SslClientAuthenticationOptions {
                    ApplicationProtocols = [ new SslApplicationProtocol("doq") ],
                    EnabledSslProtocols = SslProtocols.Tls13
                }
            };

            try {
                QuicConnection quicConnection;
                try {
                    quicConnection = await QuicConnectionFactory(options, cancellationToken).ConfigureAwait(false);
                } catch (QuicException ex) {
                    var failureResponse = new DnsResponse {
                        Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverQuic, Type = type, OriginalName = name } ],
                        Status = DnsResponseCode.ServerFailure
                    };
                    failureResponse.AddServerDetails(endpointConfiguration);
                    failureResponse.Error = ex.Message;
                    return failureResponse;
                }

                QuicStream? stream = null;
                try {
                    stream = await StreamFactory(quicConnection, cancellationToken).ConfigureAwait(false);

                    await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                    stream.CompleteWrites();

                    var lengthBuffer = new byte[2];
                    await DnsWire.ReadExactAsync(stream, lengthBuffer, 0, 2, cancellationToken).ConfigureAwait(false);
                    if (BitConverter.IsLittleEndian) {
                        Array.Reverse(lengthBuffer);
                    }
                    int responseLength = BitConverter.ToUInt16(lengthBuffer, 0);
                    var responseBuffer = new byte[responseLength];
                    await DnsWire.ReadExactAsync(stream, responseBuffer, 0, responseLength, cancellationToken).ConfigureAwait(false);

                    var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
                    response.AddServerDetails(endpointConfiguration);
                    return response;
                } finally {
                    if (stream is not null) {
                        await StreamDisposer(stream).ConfigureAwait(false);
                    }
                    await ConnectionDisposer(quicConnection).ConfigureAwait(false);
                }
            } catch (PlatformNotSupportedException ex) {
                var response = new DnsResponse {
                    Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverQuic, Type = type, OriginalName = name } ],
                    Status = DnsResponseCode.NotImplemented
                };
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"DNS over QUIC is not supported on this platform: {ex.Message}";
                return response;
            } catch (Exception ex) {
                DnsResponseCode responseCode = ex is TimeoutException ? DnsResponseCode.ServerFailure : DnsResponseCode.Refused;
                var response = new DnsResponse {
                    Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverQuic, Type = type, OriginalName = name } ],
                    Status = responseCode
                };
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"Failed to query type {type} of \"{name}\" => {ex.Message}";
                return response;
            }
        }
    }
    #pragma warning restore CA2252
#endif
}
