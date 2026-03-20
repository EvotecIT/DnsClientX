using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Implements DNS over TLS (DoT) resolution using raw wire format messages.
    /// </summary>
    internal static class DnsWireResolveDot {
        /// <summary>
        /// Sends a DNS query in wire format using DNS over TLS (DoT) and returns the response.
        /// </summary>
        /// <param name="dnsServer"></param>
        /// <param name="port"></param>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="requestDnsSec">if set to <c>true</c> [request DNS sec].</param>
        /// <param name="validateDnsSec">if set to <c>true</c> [validate DNS sec].</param>
        /// <param name="debug">if set to <c>true</c> [debug].</param>
        /// <param name="endpointConfiguration">Configuration used for server details.</param>
        /// <param name="ignoreCertificateErrors">Ignore certificate validation errors.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The DNS response.</returns>
        /// <exception cref="System.ArgumentNullException">name - Name is null or empty.</exception>
        /// <exception cref="System.Exception">
        /// Failed to read the length prefix of the response.
        /// or
        /// The stream was closed before the entire response could be read.
        /// </exception>
        internal static async Task<DnsResponse> ResolveWireFormatDoT(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, bool ignoreCertificateErrors, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var query = DnsWireQueryBuilder.BuildQuery(name, type, requestDnsSec, endpointConfiguration);
            var queryBytes = query.SerializeDnsWireFormat();

            // Calculate the length prefix for the query
            var lengthPrefix = BitConverter.GetBytes((ushort)queryBytes.Length);
            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lengthPrefix); // Ensure big-endian order
            }

            // Combine the length prefix and the query bytes
            var combinedQueryBytes = new byte[lengthPrefix.Length + queryBytes.Length];
            Buffer.BlockCopy(lengthPrefix, 0, combinedQueryBytes, 0, lengthPrefix.Length);
            Buffer.BlockCopy(queryBytes, 0, combinedQueryBytes, lengthPrefix.Length, queryBytes.Length);

            if (debug) {
                // Print the combined DNS query bytes to the logger
                Settings.Logger.WriteDebug($"Query Name: " + name + " type: " + type);
                Settings.Logger.WriteDebug($"Query before combination: {BitConverter.ToString(queryBytes)}");
                Settings.Logger.WriteDebug($"Sending combined query: {BitConverter.ToString(combinedQueryBytes)}");

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

            DotFailurePhase failurePhase = DotFailurePhase.Connect;
            try {
                // Create a new TCP client and connect to the DNS server
                using var client = new TcpClient();
                await ConnectAsync(client, dnsServer, port, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);

                // Create a new SSL stream for the secure connection
                failurePhase = DotFailurePhase.Authenticate;
                using var sslStream = new SslStream(client.GetStream(), false, (sender, certificate, chain, sslPolicyErrors) =>
                    sslPolicyErrors == SslPolicyErrors.None || ignoreCertificateErrors);

#if NET6_0_OR_GREATER
                await AuthenticateWithTimeoutAsync(sslStream, dnsServer, SslProtocols.Tls12 | SslProtocols.Tls13, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);
#else
                await AuthenticateWithTimeoutAsync(sslStream, dnsServer, SslProtocols.Tls12, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);
#endif

                // Write the combined query bytes to the SSL stream and flush it
                failurePhase = DotFailurePhase.Exchange;
                await WriteWithTimeoutAsync(sslStream, combinedQueryBytes, 0, combinedQueryBytes.Length, endpointConfiguration.TimeOut, cancellationToken, $"Writing DoT query to {dnsServer}:{port}").ConfigureAwait(false);
                await FlushWithTimeoutAsync(sslStream, endpointConfiguration.TimeOut, cancellationToken, $"Flushing DoT query to {dnsServer}:{port}").ConfigureAwait(false);

                // Prepare to read the response with handling for length prefix
                var lengthPrefixBuffer = new byte[2];
                await ReadExactWithTimeoutAsync(sslStream, lengthPrefixBuffer, 0, lengthPrefixBuffer.Length, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);
                int responseLength = (lengthPrefixBuffer[0] << 8) + lengthPrefixBuffer[1]; // Calculate total response length

                var responseBuffer = new byte[responseLength];
                await ReadExactWithTimeoutAsync(sslStream, responseBuffer, 0, responseBuffer.Length, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);

                // Deserialize the response from DNS wire format
                var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
                response.AddServerDetails(endpointConfiguration);
                return response;
            } catch (Exception ex) {
                var (status, errorCode) = MapFailure(ex, failurePhase);
                var failureResponse = new DnsResponse {
                    Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverTLS, Type = type, OriginalName = name } ],
                    Status = status,
                    ErrorCode = errorCode,
                    Exception = ex
                };
                failureResponse.AddServerDetails(endpointConfiguration);
                failureResponse.Error = $"Failed to query type {type} of \"{name}\" => {ex.Message}";
                throw new DnsClientException(failureResponse.Error!, ex) { Response = failureResponse };
            }
        }

        /// <summary>
        /// Connects asynchronously to the specified host using a <see cref="TcpClient"/>.
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

        private static async Task AuthenticateWithTimeoutAsync(SslStream sslStream, string host, SslProtocols protocols, int timeoutMilliseconds, CancellationToken cancellationToken) {
            await WaitWithTimeoutAsync(
                sslStream.AuthenticateAsClientAsync(host, null, protocols, false),
                timeoutMilliseconds,
                cancellationToken,
                $"TLS authentication with {host}").ConfigureAwait(false);
        }

        private static async Task WriteWithTimeoutAsync(Stream stream, byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken cancellationToken, string operationName) {
            await WaitWithTimeoutAsync(
                stream.WriteAsync(buffer, offset, count, cancellationToken),
                timeoutMilliseconds,
                cancellationToken,
                operationName).ConfigureAwait(false);
        }

        private static async Task FlushWithTimeoutAsync(Stream stream, int timeoutMilliseconds, CancellationToken cancellationToken, string operationName) {
            await WaitWithTimeoutAsync(
                stream.FlushAsync(cancellationToken),
                timeoutMilliseconds,
                cancellationToken,
                operationName).ConfigureAwait(false);
        }

        private static async Task ReadExactWithTimeoutAsync(Stream stream, byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken cancellationToken) {
#if NET5_0_OR_GREATER || NET472 || NETSTANDARD2_0
            if (stream.CanTimeout) {
                stream.ReadTimeout = timeoutMilliseconds;
            }
#endif
            await WaitWithTimeoutAsync(
                DnsWire.ReadExactAsync(stream, buffer, offset, count, cancellationToken),
                timeoutMilliseconds,
                cancellationToken,
                "Reading from the TLS stream").ConfigureAwait(false);
        }

        private static async Task WaitWithTimeoutAsync(Task task, int timeoutMilliseconds, CancellationToken cancellationToken, string operationName) {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMilliseconds <= 0) {
                linkedCts.Cancel();
            } else {
                linkedCts.CancelAfter(timeoutMilliseconds);
            }

            try {
                var timeoutTask = Task.Delay(Timeout.Infinite, linkedCts.Token);
                var completed = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
                if (completed != task) {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException($"{operationName} timed out after {timeoutMilliseconds} milliseconds.");
                }

                await task.ConfigureAwait(false);
            } catch (OperationCanceledException) {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"{operationName} timed out after {timeoutMilliseconds} milliseconds.");
            }
        }

        private static (DnsResponseCode Status, DnsQueryErrorCode ErrorCode) MapFailure(Exception ex, DotFailurePhase phase) {
            return ex switch {
                TimeoutException => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.Timeout),
                SocketException => phase == DotFailurePhase.Connect
                    ? (DnsResponseCode.Refused, DnsQueryErrorCode.Network)
                    : (DnsResponseCode.ServerFailure, DnsQueryErrorCode.InvalidResponse),
                AuthenticationException => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.ServFail),
                EndOfStreamException => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.InvalidResponse),
                IOException ioEx when ioEx.InnerException is SocketException => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.InvalidResponse),
                IOException => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.InvalidResponse),
                _ => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.ServFail)
            };
        }

        private enum DotFailurePhase {
            Connect,
            Authenticate,
            Exchange
        }
    }
}
