using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace DnsClientX {
    internal class DnsWireResolveUdp {
        /// <summary>
        /// Sends a DNS query in wire format using DNS over UDP (53) and returns the response.
        /// </summary>
        /// <param name="dnsServer"></param>
        /// <param name="port"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="requestDnsSec"></param>
        /// <param name="validateDnsSec"></param>
        /// <param name="debug"></param>
        /// <param name="endpointConfiguration">Provide configuration so it can be added to Question for display purposes</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The DNS response.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        internal static async Task<DnsResponse> ResolveWireFormatUdp(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var edns = endpointConfiguration.EdnsOptions;
            bool enableEdns = edns?.EnableEdns ?? endpointConfiguration.EnableEdns;
            int udpSize = edns?.UdpBufferSize ?? endpointConfiguration.UdpBufferSize;
            string? subnet = edns?.Subnet ?? endpointConfiguration.Subnet;
            var query = new DnsMessage(name, type, requestDnsSec, enableEdns, udpSize, subnet, endpointConfiguration.CheckingDisabled);
            var queryBytes = query.SerializeDnsWireFormat();

            if (debug) {
                // Print the DNS query bytes to the logger
                Settings.Logger.WriteDebug($"Query Name: " + name + " type: " + type);
                Settings.Logger.WriteDebug($"Sending query: {BitConverter.ToString(queryBytes)}");

                Settings.Logger.WriteDebug($"Transaction ID: {BitConverter.ToString(queryBytes, 0, 2)}");
                Settings.Logger.WriteDebug($"Flags: {BitConverter.ToString(queryBytes, 2, 2)}");
                Settings.Logger.WriteDebug($"Question count: {BitConverter.ToString(queryBytes, 4, 2)}");
                Settings.Logger.WriteDebug($"Answer count: {BitConverter.ToString(queryBytes, 6, 2)}");
                Settings.Logger.WriteDebug($"Authority records count: {BitConverter.ToString(queryBytes, 8, 2)}");
                Settings.Logger.WriteDebug($"Additional records count: {BitConverter.ToString(queryBytes, 10, 2)}");
                Settings.Logger.WriteDebug($"Question name: {BitConverter.ToString(queryBytes, 12, queryBytes.Length - 12 - 4)}");
                Settings.Logger.WriteDebug($"Question type: {BitConverter.ToString(queryBytes, queryBytes.Length - 4, 2)}");
                Settings.Logger.WriteDebug($"Question class: {BitConverter.ToString(queryBytes, queryBytes.Length - 2, 2)}");
            }

            try {
                // Send the DNS query over UDP and receive the response
                using var udpClient = new UdpClient();
                var responseBuffer = await SendQueryOverUdp(udpClient, queryBytes, dnsServer, port, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);

                // Deserialize the response from DNS wire format
                var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
                if (response.IsTruncated && endpointConfiguration.UseTcpFallback) {
                    // If the response is truncated and fallback is enabled, retry the query over TCP
                    response = await DnsWireResolveTcp.ResolveWireFormatTcp(dnsServer, port, name, type, requestDnsSec,
                        validateDnsSec, debug, endpointConfiguration, cancellationToken).ConfigureAwait(false);
                }
                response.AddServerDetails(endpointConfiguration);
                return response;
            } catch (Exception ex) {
                DnsResponseCode responseCode;
                if (ex.InnerException is WebException webEx && webEx.Status == WebExceptionStatus.ConnectFailure) {
                    responseCode = DnsResponseCode.Refused;
                } else if (ex is TimeoutException) {
                    responseCode = DnsResponseCode.ServerFailure;
                } else {
                    responseCode = DnsResponseCode.ServerFailure;
                }

                DnsResponse response = new DnsResponse {
                    Questions = [
                        new DnsQuestion() {
                    Name = name,
                    RequestFormat = DnsRequestFormat.DnsOverUDP,
                    Type = type,
                    OriginalName = name
                }
                    ],
                    Status = responseCode
                };
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"Failed to query type {type} of \"{name}\" => {ex.Message + " " + ex.InnerException?.Message}";
                return response;
            }
        }


        /// <summary>
        /// Sends a DNS query over UDP and returns the response.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="dnsServer"></param>
        /// <param name="port"></param>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>Raw DNS response bytes.</returns>
        private static async Task<byte[]> SendQueryOverUdp(UdpClient udpClient, byte[] query, string dnsServer, int port, int timeoutMilliseconds, CancellationToken cancellationToken) {
            // Set the server IP address and port number
            var serverEndpoint = new IPEndPoint(IPAddress.Parse(dnsServer), port);

                // Send the query
#if NET5_0_OR_GREATER
                await udpClient.SendAsync(query, serverEndpoint, cancellationToken).ConfigureAwait(false);
#else
                await udpClient.SendAsync(query, query.Length, serverEndpoint).ConfigureAwait(false);
#endif

                // Set up the cancellation token for the timeout
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
                    cts.CancelAfter(timeoutMilliseconds);
                    try {
                        // Receive the response with a timeout
#if NET5_0_OR_GREATER
                        var responseTask = udpClient.ReceiveAsync(cancellationToken).AsTask();
#else
                        var responseTask = udpClient.ReceiveAsync();
#endif
                        var completedTask = await Task.WhenAny(responseTask, Task.Delay(timeoutMilliseconds, cts.Token)).ConfigureAwait(false);

                        if (completedTask == responseTask) {
                            // If the response task completed, return the response buffer
                            return responseTask.Result.Buffer;
                        } else {
                            // If the timeout task completed, throw a timeout exception
                            throw new TimeoutException("The UDP query timed out.");
                        }
                    } catch (OperationCanceledException) {
                        throw new TimeoutException("The UDP query timed out.");
                    }
                }
            }
        }
    }
