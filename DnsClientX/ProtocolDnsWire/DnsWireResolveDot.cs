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

            try {
                // Create a new TCP client and connect to the DNS server
                using var client = new TcpClient();
                await ConnectAsync(client, dnsServer, port, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);

                // Create a new SSL stream for the secure connection
                using var sslStream = new SslStream(client.GetStream(), false, (sender, certificate, chain, sslPolicyErrors) =>
                    sslPolicyErrors == SslPolicyErrors.None || ignoreCertificateErrors);

#if NET6_0_OR_GREATER
                await sslStream.AuthenticateAsClientAsync(dnsServer, null, SslProtocols.Tls12 | SslProtocols.Tls13, false).ConfigureAwait(false);
#else
                await sslStream.AuthenticateAsClientAsync(dnsServer, null, SslProtocols.Tls12, false).ConfigureAwait(false);
#endif

                // Write the combined query bytes to the SSL stream and flush it
                await sslStream.WriteAsync(combinedQueryBytes, 0, combinedQueryBytes.Length, cancellationToken).ConfigureAwait(false);
                await sslStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                // Prepare to read the response with handling for length prefix
                var lengthPrefixBuffer = new byte[2];
                int prefixBytesRead = await sslStream.ReadAsync(lengthPrefixBuffer, 0, 2, cancellationToken).ConfigureAwait(false);
                if (prefixBytesRead != 2) {
                    throw new Exception("Failed to read the length prefix of the response.");
                }
                int responseLength = (lengthPrefixBuffer[0] << 8) + lengthPrefixBuffer[1]; // Calculate total response length

                var responseBuffer = new byte[responseLength];
                int totalBytesRead = 0;
                while (totalBytesRead < responseLength) {
                    int bytesRead = await sslStream.ReadAsync(responseBuffer, totalBytesRead, responseLength - totalBytesRead, cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0) {
                        throw new Exception("The stream was closed before the entire response could be read.");
                    }
                    totalBytesRead += bytesRead;
                }

                // Deserialize the response from DNS wire format
                var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
                response.AddServerDetails(endpointConfiguration);
                return response;
            } catch (Exception ex) {
                var failureResponse = new DnsResponse {
                    Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverTLS, Type = type, OriginalName = name } ],
                    Status = ex is TimeoutException ? DnsResponseCode.ServerFailure : DnsResponseCode.Refused
                };
                failureResponse.AddServerDetails(endpointConfiguration);
                failureResponse.Error = $"Failed to query type {type} of \"{name}\" => {ex.Message}";
                throw new DnsClientException(failureResponse.Error!, failureResponse);
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
    }
}
