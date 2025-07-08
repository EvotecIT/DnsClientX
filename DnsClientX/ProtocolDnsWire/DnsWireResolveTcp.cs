using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

namespace DnsClientX {
    internal class DnsWireResolveTcp {
        /// <summary>
        /// Sends a DNS query in wire format using DNS over TCP (53) and returns the response.
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
        internal static async Task<DnsResponse> ResolveWireFormatTcp(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
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
                // Send the DNS query over TCP and receive the response
                var responseBuffer = await SendQueryOverTcp(queryBytes, dnsServer, port, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);

                // Deserialize the response from DNS wire format
                var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
                response.AddServerDetails(endpointConfiguration);
                return response;
            } catch (Exception ex) {
                DnsResponseCode responseCode;
                if (ex is SocketException) {
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
                            RequestFormat = DnsRequestFormat.DnsOverTCP,
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
        /// Sends a DNS query over TCP and returns the response.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="dnsServer"></param>
        /// <param name="port"></param>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>Raw DNS response bytes.</returns>
        private static async Task<byte[]> SendQueryOverTcp(byte[] query, string dnsServer, int port, int timeoutMilliseconds, CancellationToken cancellationToken) {
            var tcpClient = new TcpClient();
            NetworkStream? stream = null;
            try {
                // Connect to the server with timeout
                await ConnectAsync(tcpClient, dnsServer, port, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);

                // Stream operations are wrapped in try/finally to ensure disposal
                stream = tcpClient.GetStream();

                // Write the length of the query as a 16-bit big-endian integer
                var lengthBytes = BitConverter.GetBytes((ushort)query.Length);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(lengthBytes); // Ensure big-endian order
                }

                // Write operations with timeout
                var writeTask = stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
                var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
                var completedTask = await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask) {
                    throw new TimeoutException($"Writing length to {dnsServer}:{port} timed out after {timeoutMilliseconds} milliseconds.");
                }
                await writeTask.ConfigureAwait(false);

                // Write the query
                writeTask = stream.WriteAsync(query, 0, query.Length, cancellationToken);
                timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
                completedTask = await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask) {
                    throw new TimeoutException($"Writing query to {dnsServer}:{port} timed out after {timeoutMilliseconds} milliseconds.");
                }
                await writeTask.ConfigureAwait(false);

                // Read the length of the response with timeout
                lengthBytes = new byte[2];
                var readTask = ReadExactWithTimeoutAsync(stream, lengthBytes, 0, lengthBytes.Length, timeoutMilliseconds, cancellationToken);
                await readTask.ConfigureAwait(false);

                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(lengthBytes); // Ensure big-endian order
                }
                var responseLength = BitConverter.ToUInt16(lengthBytes, 0);

                // Read the response with timeout
                var responseBuffer = new byte[responseLength];
                readTask = ReadExactWithTimeoutAsync(stream, responseBuffer, 0, responseBuffer.Length, timeoutMilliseconds, cancellationToken);
                await readTask.ConfigureAwait(false);

                return responseBuffer;
            } catch (OperationCanceledException) {
                throw new TimeoutException($"The TCP DNS query timed out after {timeoutMilliseconds} milliseconds.");
            } finally {
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                if (stream is not null) {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
#else
                stream?.Dispose();
#endif
                tcpClient.Close();
                tcpClient.Dispose();
            }
        }

        /// <summary>
        /// Helper to read exactly the requested number of bytes from a stream with timeout.
        /// </summary>
        private static async Task ReadExactWithTimeoutAsync(Stream stream, byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken cancellationToken) {
            var readTask = DnsWire.ReadExactAsync(stream, buffer, offset, count, cancellationToken);
            var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
            var completedTask = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask) {
                throw new TimeoutException($"Reading from stream timed out after {timeoutMilliseconds} milliseconds.");
            }

            await readTask.ConfigureAwait(false); // Ensure any exceptions from read are propagated
        }

        /// <summary>
        /// Connects the provided <see cref="TcpClient"/> with support for timeout and cancellation on older frameworks.
        /// </summary>
        /// <param name="tcpClient">Client used for the connection.</param>
        /// <param name="host">Target host.</param>
        /// <param name="port">Target port.</param>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        private static async Task ConnectAsync(TcpClient tcpClient, string host, int port, int timeoutMilliseconds, CancellationToken cancellationToken) {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMilliseconds <= 0) {
                linkedCts.Cancel();
            } else {
                linkedCts.CancelAfter(timeoutMilliseconds);
            }
#if NET5_0_OR_GREATER
            try {
                await tcpClient.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                tcpClient.Close();
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
#else
            var connectTask = tcpClient.ConnectAsync(host, port);
            var delayTask = Task.Delay(Timeout.Infinite, linkedCts.Token);

            var completed = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
            if (completed != connectTask) {
                tcpClient.Close();
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }

            await connectTask.ConfigureAwait(false); // propagate possible exceptions
#endif
        }
    }
}
