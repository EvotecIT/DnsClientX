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
    internal static class DnsWireResolveQuic {
        internal static Func<string, IPHostEntry>? HostEntryResolver;
        internal static async Task<DnsResponse> ResolveWireFormatQuic(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var query = new DnsMessage(name, type, requestDnsSec, endpointConfiguration.EnableEdns, endpointConfiguration.UdpBufferSize, endpointConfiguration.Subnet);
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

            await using var connection = await QuicConnection.ConnectAsync(options, cancellationToken);
            await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken);

            try {

                await stream.WriteAsync(payload, cancellationToken);
                stream.CompleteWrites();

                var lengthBuffer = new byte[2];
                await DnsWire.ReadExactAsync(stream, lengthBuffer, 0, 2, cancellationToken);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(lengthBuffer);
                }
                int responseLength = BitConverter.ToUInt16(lengthBuffer, 0);
                var responseBuffer = new byte[responseLength];
                await DnsWire.ReadExactAsync(stream, responseBuffer, 0, responseLength, cancellationToken);

                var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer);
                response.AddServerDetails(endpointConfiguration);
                return response;
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
