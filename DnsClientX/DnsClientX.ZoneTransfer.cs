using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class providing zone transfer functionality.
    /// </summary>
    public partial class ClientX {
        /// <summary>
        /// Performs a DNS zone transfer (AXFR) using TCP.
        /// </summary>
        /// <param name="zone">Zone name to transfer.</param>
        /// <param name="retryOnTransient">Whether to retry on transient failures.</param>
        /// <param name="maxRetries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ordered RRsets as returned by the server.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="DnsClientException">When the transfer fails.</exception>
        public async Task<DnsAnswer[][]> ZoneTransferAsync(
            string zone,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            CancellationToken cancellationToken = default) {
            if (string.IsNullOrEmpty(zone)) {
                throw new ArgumentNullException(nameof(zone));
            }

            if (cancellationToken.IsCancellationRequested) {
                return await Task.FromCanceled<DnsAnswer[][]>(cancellationToken).ConfigureAwait(false);
            }

            EndpointConfiguration.SelectHostNameStrategy();

            var query = new DnsMessage(zone, DnsRecordType.AXFR, requestDnsSec: false, enableEdns: false, EndpointConfiguration.UdpBufferSize, null, EndpointConfiguration.CheckingDisabled, EndpointConfiguration.SigningKey);
            var queryBytes = query.SerializeDnsWireFormat();

            async Task<List<byte[]>> Execute() => await SendAxfrOverTcp(queryBytes, EndpointConfiguration.Hostname, EndpointConfiguration.Port, EndpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);

            List<byte[]> responses;
            try {
                responses = retryOnTransient && maxRetries > 1
                    ? await RetryAsync(
                        Execute,
                        maxRetries,
                        retryDelayMs,
                        EndpointConfiguration.SelectionStrategy == DnsSelectionStrategy.Failover ? EndpointConfiguration.AdvanceToNextHostname : null).ConfigureAwait(false)
                    : await Execute().ConfigureAwait(false);
            } catch (DnsClientException) {
                throw;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                throw new DnsClientException($"Zone transfer failed: {ex.Message}");
            }

            var records = new List<DnsAnswer>();
            int soaCount = 0;
            foreach (var buffer in responses) {
                var res = await DnsWire.DeserializeDnsWireFormat(null, Debug, buffer).ConfigureAwait(false);
                res.AddServerDetails(EndpointConfiguration);
                if (res.Status != DnsResponseCode.NoError) {
                    throw new DnsClientException($"Zone transfer failed with {res.Status}", res);
                }
                if (res.Answers != null) {
                    records.AddRange(res.Answers);
                    soaCount += res.Answers.Count(a => a.Type == DnsRecordType.SOA);
                    if (soaCount >= 2) break;
                }
            }

            if (soaCount == 0) {
                return Array.Empty<DnsAnswer[]>();
            }

            if (soaCount < 2) {
                throw new DnsClientException("Zone transfer incomplete: closing SOA record missing.");
            }

            var lastResponse = await DnsWire.DeserializeDnsWireFormat(null, Debug, responses[responses.Count - 1]).ConfigureAwait(false);
            lastResponse.AddServerDetails(EndpointConfiguration);
            if (lastResponse.Answers == null || lastResponse.Answers.Length == 0 || lastResponse.Answers[lastResponse.Answers.Length - 1].Type != DnsRecordType.SOA) {
                throw new DnsClientException("Zone transfer incomplete: closing SOA record missing.");
            }

            var rrsets = new List<List<DnsAnswer>>();
            foreach (var rec in records) {
                if (rrsets.Count == 0 || rrsets[rrsets.Count - 1][0].Name != rec.Name || rrsets[rrsets.Count - 1][0].Type != rec.Type) {
                    rrsets.Add(new List<DnsAnswer> { rec });
                } else {
                    rrsets[rrsets.Count - 1].Add(rec);
                }
            }

            return rrsets.Select(r => r.ToArray()).ToArray();
        }

        /// <summary>
        /// Performs a DNS zone transfer (AXFR) synchronously.
        /// </summary>
        /// <param name="zone">Zone name to transfer.</param>
        /// <param name="retryOnTransient">Whether to retry on transient failures.</param>
        /// <param name="maxRetries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ordered RRsets as returned by the server.</returns>
        public DnsAnswer[][] ZoneTransferSync(
            string zone,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            CancellationToken cancellationToken = default) {
            return ZoneTransferAsync(zone, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).RunSync(cancellationToken);
        }

        /// <summary>
        /// Performs a DNS zone transfer (AXFR) using TCP and streams the RRsets as they are processed.
        /// </summary>
        /// <param name="zone">Zone name to transfer.</param>
        /// <param name="retryOnTransient">Whether to retry on transient failures.</param>
        /// <param name="maxRetries">Maximum number of retry attempts.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An asynchronous enumeration of ordered RRsets.</returns>
        public async IAsyncEnumerable<DnsAnswer[]> ZoneTransferStreamAsync(
            string zone,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var rrsets = await ZoneTransferAsync(zone, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).ConfigureAwait(false);
            foreach (var rrset in rrsets) {
                yield return rrset;
            }
        }

        private static async Task<List<byte[]>> SendAxfrOverTcp(byte[] query, string dnsServer, int port, int timeoutMilliseconds, CancellationToken cancellationToken) {
            TcpClient tcpClient = new();
            try {
                await ConnectAsync(tcpClient, dnsServer, port, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                NetworkStream stream = tcpClient.GetStream();
                try {
                    var lengthBytes = BitConverter.GetBytes((ushort)query.Length);
                    if (BitConverter.IsLittleEndian) {
                        Array.Reverse(lengthBytes);
                    }

                    var writeTask = stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
                    var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
                    if (await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false) == timeoutTask) {
                        throw new TimeoutException($"Writing length to {dnsServer}:{port} timed out after {timeoutMilliseconds} milliseconds.");
                    }
                    await writeTask.ConfigureAwait(false);

                    writeTask = stream.WriteAsync(query, 0, query.Length, cancellationToken);
                    timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
                    if (await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false) == timeoutTask) {
                        throw new TimeoutException($"Writing query to {dnsServer}:{port} timed out after {timeoutMilliseconds} milliseconds.");
                    }
                    await writeTask.ConfigureAwait(false);

                    var responses = new List<byte[]>();
                    var lenBuf = new byte[2];
                    while (true) {
                        try {
                            await ReadExactWithTimeoutAsync(stream, lenBuf, 0, 2, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                        } catch (EndOfStreamException) when (responses.Count == 0) {
                            throw new DnsClientException("Connection closed during zone transfer.");
                        } catch (EndOfStreamException) {
                            break;
                        }
                        if (BitConverter.IsLittleEndian) {
                            Array.Reverse(lenBuf);
                        }
                        int length = BitConverter.ToUInt16(lenBuf, 0);
                        var responseBuffer = new byte[length];
                        await ReadExactWithTimeoutAsync(stream, responseBuffer, 0, length, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                        responses.Add(responseBuffer);
                    }

                    return responses;
                } finally {
                    stream.Close();
                    stream.Dispose();
                }
            } finally {
                tcpClient.Close();
                tcpClient.Dispose();
            }
        }

        private static async Task ReadExactWithTimeoutAsync(NetworkStream stream, byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken cancellationToken) {
            var readTask = DnsWire.ReadExactAsync(stream, buffer, offset, count, cancellationToken);
            var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
            if (await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false) == timeoutTask) {
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
                tcpClient.Close();
                tcpClient.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
#else
            var connectTask = tcpClient.ConnectAsync(host, port);
            var delayTask = Task.Delay(Timeout.Infinite, linkedCts.Token);
            var completed = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
            if (completed != connectTask) {
                tcpClient.Close();
                tcpClient.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
            await connectTask.ConfigureAwait(false);
#endif
        }
    }
}
