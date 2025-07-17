using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Sends DNS UPDATE messages using TCP transport.
    /// </summary>
    internal static class DnsWireUpdateTcp {
        private static async Task<byte[]> SendMessageOverTcp(byte[] message, string dnsServer, int port, int timeoutMilliseconds, CancellationToken cancellationToken) {
            using TcpClient tcpClient = DnsWireResolveTcp.TcpClientFactory();
            NetworkStream? stream = null;
            try {
                await ConnectAsync(tcpClient, dnsServer, port, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                stream = tcpClient.GetStream();
                var lengthBytes = BitConverter.GetBytes((ushort)message.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
                await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(message, 0, message.Length, cancellationToken).ConfigureAwait(false);
                lengthBytes = new byte[2];
                await ReadExactWithTimeoutAsync(stream, lengthBytes, 0, 2, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
                var responseLength = BitConverter.ToUInt16(lengthBytes, 0);
                var responseBuffer = new byte[responseLength];
                await ReadExactWithTimeoutAsync(stream, responseBuffer, 0, responseBuffer.Length, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                return responseBuffer;
            } finally {
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                if (stream is not null) await stream.DisposeAsync().ConfigureAwait(false);
#else
                stream?.Dispose();
#endif
            }
        }

        /// <summary>
        /// Sends a DNS UPDATE message to add or modify a record.
        /// </summary>
        /// <param name="dnsServer">Server address.</param>
        /// <param name="port">Server port.</param>
        /// <param name="zone">Zone to update.</param>
        /// <param name="name">Record name.</param>
        /// <param name="type">Record type.</param>
        /// <param name="data">Record data.</param>
        /// <param name="ttl">Record TTL.</param>
        /// <param name="debug">Whether debugging is enabled.</param>
        /// <param name="endpointConfiguration">Endpoint settings.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Response from the DNS server.</returns>
        internal static async Task<DnsResponse> UpdateRecordAsync(string dnsServer, int port, string zone, string name, DnsRecordType type, string data, int ttl, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            var message = DnsUpdateMessage.CreateAddMessage(zone, name, type, data, ttl);
            var responseBuffer = await SendMessageOverTcp(message, dnsServer, port, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);
            var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
            response.AddServerDetails(endpointConfiguration);
            return response;
        }

        /// <summary>
        /// Sends a DNS UPDATE message to delete a record.
        /// </summary>
        /// <param name="dnsServer">Server address.</param>
        /// <param name="port">Server port.</param>
        /// <param name="zone">Zone containing the record.</param>
        /// <param name="name">Record name.</param>
        /// <param name="type">Record type.</param>
        /// <param name="debug">Whether debugging is enabled.</param>
        /// <param name="endpointConfiguration">Endpoint settings.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Response from the DNS server.</returns>
        internal static async Task<DnsResponse> DeleteRecordAsync(string dnsServer, int port, string zone, string name, DnsRecordType type, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            var message = DnsUpdateMessage.CreateDeleteMessage(zone, name, type);
            var responseBuffer = await SendMessageOverTcp(message, dnsServer, port, endpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);
            var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
            response.AddServerDetails(endpointConfiguration);
            return response;
        }

        private static async Task ReadExactWithTimeoutAsync(Stream stream, byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken cancellationToken) {
            var readTask = DnsWire.ReadExactAsync(stream, buffer, offset, count, cancellationToken);
            var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
            var completedTask = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask) {
                throw new TimeoutException($"Reading from stream timed out after {timeoutMilliseconds} milliseconds.");
            }

            await readTask.ConfigureAwait(false);
        }

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
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
#else
            var connectTask = tcpClient.ConnectAsync(host, port);
            var delayTask = Task.Delay(Timeout.Infinite, linkedCts.Token);

            var completed = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
            if (completed != connectTask) {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }

            await connectTask.ConfigureAwait(false);
#endif
        }
    }
}
