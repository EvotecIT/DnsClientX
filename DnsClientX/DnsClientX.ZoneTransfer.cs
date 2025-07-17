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
    /// <remarks>
    /// Zone transfers retrieve all records in a zone using the AXFR protocol.
    /// </remarks>
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
        public async Task<ZoneTransferResult[]> ZoneTransferAsync(
            string zone,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            CancellationToken cancellationToken = default) {
            if (string.IsNullOrEmpty(zone)) {
                throw new ArgumentNullException(nameof(zone));
            }

            if (cancellationToken.IsCancellationRequested) {
                return await Task.FromCanceled<ZoneTransferResult[]>(cancellationToken).ConfigureAwait(false);
            }

            var results = new List<ZoneTransferResult>();
            try {
                await foreach (var rrset in ZoneTransferStreamAsync(zone, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).ConfigureAwait(false)) {
                    results.Add(rrset);
                }
            } catch (DnsClientException) {
                throw;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                throw new DnsClientException($"Zone transfer failed: {ex.Message}");
            }

            return results.ToArray();
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
        public ZoneTransferResult[] ZoneTransferSync(
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
        public async IAsyncEnumerable<ZoneTransferResult> ZoneTransferStreamAsync(
            string zone,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if (string.IsNullOrEmpty(zone)) {
                throw new ArgumentNullException(nameof(zone));
            }

            EndpointConfiguration.SelectHostNameStrategy();

            var query = new DnsMessage(zone, DnsRecordType.AXFR, requestDnsSec: false, enableEdns: false, EndpointConfiguration.UdpBufferSize, null, EndpointConfiguration.CheckingDisabled, EndpointConfiguration.SigningKey);
            var queryBytes = query.SerializeDnsWireFormat();

            for (int attempt = 0; attempt < maxRetries; attempt++) {
                cancellationToken.ThrowIfCancellationRequested();
                await using var enumerator = SendAxfrOverTcp(queryBytes, EndpointConfiguration.Hostname!, EndpointConfiguration.Port, EndpointConfiguration.TimeOut, Debug, EndpointConfiguration, cancellationToken).GetAsyncEnumerator(cancellationToken);
                Exception? iterationException = null;

                while (true) {
                    bool moved;
                    try {
                        moved = await enumerator.MoveNextAsync();
                    } catch (Exception ex) {
                        iterationException = ex;
                        break;
                    }

                    if (!moved) {
                        iterationException = null;
                        break;
                    }

                    yield return enumerator.Current;
                }

                if (iterationException == null) {
                    yield break;
                }

                if (!(retryOnTransient && attempt < maxRetries - 1 && IsTransient(iterationException))) {
                    if (iterationException is DnsClientException || iterationException is OperationCanceledException) {
                        throw iterationException;
                    }
                    throw new DnsClientException($"Zone transfer failed: {iterationException.Message}");
                }

                if (EndpointConfiguration.SelectionStrategy == DnsSelectionStrategy.Failover) {
                    EndpointConfiguration.AdvanceToNextHostname();
                }

                int exponentialDelay = retryDelayMs <= 0 ? 0 : (int)Math.Min((long)retryDelayMs << attempt, int.MaxValue);
                int jitter = GetJitter(retryDelayMs);
                await Task.Delay(exponentialDelay + jitter, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async IAsyncEnumerable<ZoneTransferResult> SendAxfrOverTcp(
            byte[] query,
            string dnsServer,
            int port,
            int timeoutMilliseconds,
            bool debug,
            Configuration configuration,
            [EnumeratorCancellation] CancellationToken cancellationToken) {
            using TcpClient tcpClient = DnsWireResolveTcp.TcpClientFactory();
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

                    var lenBuf = new byte[2];
                    var current = new List<DnsAnswer>();
                    int soaCount = 0;
                    bool sawClosing = false;
                    bool extraAfterClosing = false;
                    bool received = false;
                    bool started = false;
                    int index = 0;
                    DnsAnswer? lastRecord = null;

                    while (true) {
                        try {
                            await ReadExactWithTimeoutAsync(stream, lenBuf, 0, 2, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                        } catch (EndOfStreamException) when (!received) {
                            throw new DnsClientException("Connection closed during zone transfer.");
                        } catch (EndOfStreamException) {
                            break;
                        }

                        received = true;
                        if (BitConverter.IsLittleEndian) {
                            Array.Reverse(lenBuf);
                        }
                        int length = BitConverter.ToUInt16(lenBuf, 0);
                        var responseBuffer = new byte[length];
                        await ReadExactWithTimeoutAsync(stream, responseBuffer, 0, length, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);

                        var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
                        response.AddServerDetails(configuration);
                        if (response.Status != DnsResponseCode.NoError) {
                            throw new DnsClientException($"Zone transfer failed with {response.Status}", response);
                        }

                        var answers = response.Answers;
                        if (sawClosing) {
                            if (answers != null && answers.Length > 0) {
                                throw new DnsClientException("Zone transfer incomplete: closing SOA record not last.");
                            }
                            extraAfterClosing = true;
                            continue;
                        }

                        if (answers == null) {
                            continue;
                        }

                        foreach (var rec in answers) {
                            if (soaCount >= 2) {
                                throw new DnsClientException("Zone transfer incomplete: closing SOA record not last.");
                            }

                            if (!started) {
                                if (rec.Type != DnsRecordType.SOA) {
                                    continue;
                                }
                                started = true;
                            }

                            if (current.Count == 0 || (current[0].Name == rec.Name && current[0].Type == rec.Type)) {
                                current.Add(rec);
                            } else {
                                bool opening = index == 0 && current[0].Type == DnsRecordType.SOA;
                                bool closing = sawClosing && current[0].Type == DnsRecordType.SOA;
                                yield return new ZoneTransferResult(current.ToArray(), opening, closing, index++);
                                current.Clear();
                                current.Add(rec);
                            }

                            lastRecord = rec;
                            if (rec.Type == DnsRecordType.SOA) {
                                soaCount++;
                                if (soaCount == 2) {
                                    sawClosing = true;
                                }
                            }
                        }
                    }

                    if (current.Count > 0 && started) {
                        bool opening = index == 0 && current[0].Type == DnsRecordType.SOA;
                        bool closing = sawClosing && current[0].Type == DnsRecordType.SOA;
                        yield return new ZoneTransferResult(current.ToArray(), opening, closing, index++);
                    }

                    if (soaCount == 0) {
                        yield break;
                    }

                    if (soaCount < 2 || extraAfterClosing || lastRecord == null || lastRecord.Value.Type != DnsRecordType.SOA) {
                        throw new DnsClientException("Zone transfer incomplete: closing SOA record missing.");
                    }
                } finally {
                    stream.Close();
                    stream.Dispose();
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
                try {
                    tcpClient.Close();
                } catch {
                    // Ignore exceptions during Close() as disposal may happen elsewhere
                }
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
#else
            var connectTask = tcpClient.ConnectAsync(host, port);
            var delayTask = Task.Delay(Timeout.Infinite, linkedCts.Token);
            var completed = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
            if (completed != connectTask) {
                try {
                    tcpClient.Close();
                } catch {
                    // Ignore exceptions during Close() as disposal may happen elsewhere
                }
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
            await connectTask.ConfigureAwait(false);
#endif
        }
    }
}
