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
        internal static async Task<DnsResponse> ResolveWireFormatUdp(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, int maxRetries, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var edns = endpointConfiguration.EdnsOptions;
            bool enableEdns = endpointConfiguration.EnableEdns;
            int udpSize = endpointConfiguration.UdpBufferSize;
            string? subnet = endpointConfiguration.Subnet;
            if (edns != null) {
                enableEdns = edns.EnableEdns;
                udpSize = edns.UdpBufferSize;
                subnet = edns.Subnet;
            }
            var query = new DnsMessage(name, type, requestDnsSec, enableEdns, udpSize, subnet, endpointConfiguration.CheckingDisabled, endpointConfiguration.SigningKey);
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

            if (!IPAddress.TryParse(dnsServer, out IPAddress address)) {
                DnsResponse invalidAddress = new DnsResponse {
                    Questions = [
                        new DnsQuestion {
                            Name = name,
                            RequestFormat = DnsRequestFormat.DnsOverUDP,
                            Type = type,
                            OriginalName = name
                        }
                    ],
                    Status = DnsResponseCode.ServerFailure
                };
                invalidAddress.AddServerDetails(endpointConfiguration);
                invalidAddress.Error = $"Invalid DNS server '{dnsServer}'.";
                return invalidAddress;
            }

            Exception lastException = null;
            for (int attempt = 1; attempt <= Math.Max(1, maxRetries); attempt++) {
                try {
                    using var udpClient = new UdpClient(address.AddressFamily);
                    var responseBuffer = await SendQueryOverUdp(udpClient, queryBytes, address, port, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);

                    var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
                    if (response.IsTruncated && endpointConfiguration.UseTcpFallback) {
                        response = await DnsWireResolveTcp.ResolveWireFormatTcp(dnsServer, port, name, type, requestDnsSec,
                            validateDnsSec, debug, endpointConfiguration, cancellationToken).ConfigureAwait(false);
                    }
                    response.AddServerDetails(endpointConfiguration);
                    return response;
                } catch (Exception ex) {
                    lastException = ex;
                    if (attempt == maxRetries) break;
                }
            }

            DnsResponseCode responseCode;
            if (lastException?.InnerException is WebException webEx && webEx.Status == WebExceptionStatus.ConnectFailure) {
                responseCode = DnsResponseCode.Refused;
            } else if (lastException is TimeoutException) {
                responseCode = DnsResponseCode.ServerFailure;
            } else {
                responseCode = DnsResponseCode.ServerFailure;
            }

            DnsResponse failure = new DnsResponse {
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
            failure.AddServerDetails(endpointConfiguration);
            failure.Error = $"Failed to query type {type} of \"{name}\" => {lastException?.Message + " " + lastException?.InnerException?.Message}";
            return failure;
        }


        /// <summary>
        /// Sends a DNS query over UDP and returns the response.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="ipAddress"></param>
        /// <param name="port"></param>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>Raw DNS response bytes.</returns>
        private static async Task<byte[]> SendQueryOverUdp(UdpClient udpClient, byte[] query, IPAddress ipAddress, int port, int timeoutMilliseconds, CancellationToken cancellationToken) {
            // Set the server IP address and port number
            if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6) {
                udpClient.Client.DualMode = true;
            }
            var serverEndpoint = new IPEndPoint(ipAddress, port);

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
