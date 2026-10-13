using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
            } catch (PlatformNotSupportedException) {
                throw;
            } catch (Exception ex) {
                throw new DnsClientException($"Zone transfer failed: {ex.Message}", ex);
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
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(zone)) {
                throw new ArgumentNullException(nameof(zone));
            }
            if (maxRetries <= 0) {
                throw new ArgumentOutOfRangeException(nameof(maxRetries), "Maximum retries must be greater than zero.");
            }
            if (retryDelayMs < 0) {
                throw new ArgumentOutOfRangeException(nameof(retryDelayMs), "Retry delay cannot be negative.");
            }

            Configuration queryConfiguration = EndpointConfiguration.CreateQuerySnapshot();
            ValidateZoneTransferConfiguration(queryConfiguration);

            var query = new DnsMessage(zone, DnsRecordType.AXFR, new DnsMessageOptions(
                CheckingDisabled: queryConfiguration.CheckingDisabled,
                RecursionDesired: false));
            byte[] queryBytes = query.SerializeDnsWireFormat();
            byte[]? requestMac = null;
            if (queryConfiguration.TsigKey != null) {
                DnsTsigSignedMessage signedQuery = DnsTsig.Sign(queryBytes, query.TransactionId, queryConfiguration.TsigKey);
                queryBytes = signedQuery.WireData;
                requestMac = signedQuery.Mac;
            }

            for (int attempt = 0; attempt < maxRetries; attempt++) {
                cancellationToken.ThrowIfCancellationRequested();
                bool yieldedAny = false;
                await using var enumerator = SendAxfrOverTcp(queryBytes, requestMac, query.TransactionId, zone, queryConfiguration.Hostname!, queryConfiguration.Port, queryConfiguration.TimeOut, Debug, queryConfiguration, IgnoreCertificateErrors, cancellationToken).GetAsyncEnumerator(cancellationToken);
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

                    yieldedAny = true;
                    yield return enumerator.Current;
                }

                if (iterationException == null) {
                    yield break;
                }

                // Once a streaming caller has observed a prefix, replaying a fresh attempt would
                // corrupt the logical AXFR with duplicate RRsets. Retries are therefore safe only
                // before the first yielded RRset.
                if (yieldedAny || !(retryOnTransient && attempt < maxRetries - 1 && IsTransient(iterationException))) {
                    if (iterationException is DnsClientException
                        || iterationException is OperationCanceledException
                        || iterationException is PlatformNotSupportedException) {
                        throw iterationException;
                    }
                    throw new DnsClientException($"Zone transfer failed: {iterationException.Message}", iterationException);
                }

                if (EndpointConfiguration.SelectionStrategy == DnsSelectionStrategy.Failover) {
                    EndpointConfiguration.AdvanceToNextHostname();
                    queryConfiguration = EndpointConfiguration.CreateQuerySnapshot();
                }

                int exponentialDelay = retryDelayMs <= 0 ? 0 : (int)Math.Min((long)retryDelayMs << attempt, int.MaxValue);
                int jitter = GetJitter(retryDelayMs);
                await Task.Delay(exponentialDelay + jitter, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async IAsyncEnumerable<ZoneTransferResult> SendAxfrOverTcp(
            byte[] query,
            byte[]? requestMac,
            ushort expectedTransactionId,
            string expectedZone,
            string dnsServer,
            int port,
            int timeoutMilliseconds,
            bool debug,
            Configuration configuration,
            bool ignoreCertificateErrors,
            [EnumeratorCancellation] CancellationToken cancellationToken) {
            await using DnsZoneTransferConnection connection = await DnsZoneTransferConnection.ConnectAsync(
                dnsServer, port, timeoutMilliseconds, configuration, ignoreCertificateErrors, cancellationToken)
                .ConfigureAwait(false);
            Stream stream = connection.Stream;
                    var lengthBytes = BitConverter.GetBytes((ushort)query.Length);
                    if (BitConverter.IsLittleEndian) {
                        Array.Reverse(lengthBytes);
                    }

                    var writeTask = stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
                    var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
                    if (await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false) == timeoutTask) {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw new TimeoutException($"Writing length to {dnsServer}:{port} timed out after {timeoutMilliseconds} milliseconds.");
                    }
                    await writeTask.ConfigureAwait(false);

                    writeTask = stream.WriteAsync(query, 0, query.Length, cancellationToken);
                    timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
                    if (await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false) == timeoutTask) {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw new TimeoutException($"Writing query to {dnsServer}:{port} timed out after {timeoutMilliseconds} milliseconds.");
                    }
                    await writeTask.ConfigureAwait(false);

                    var current = new List<DnsAnswer>();
                    var currentCanonical = new List<ZoneCanonicalRecord>();
                    int soaCount = 0;
                    bool sawClosing = false;
                    bool started = false;
                    int index = 0;
                    int recordCount = 0;
                    DnsAnswer? lastRecord = null;
                    DnsAnswer? openingSoa = null;
                    await foreach (TransferFrame frame in ReadTransferFramesAsync(stream, configuration,
                        expectedTransactionId, requestMac, cancellationToken).ConfigureAwait(false)) {
                        byte[] responseBuffer = frame.Data;

                        var response = await DnsWire.DeserializeDnsWireFormat(null, debug, responseBuffer).ConfigureAwait(false);
                        response.AddServerDetails(configuration);
                        if (response.Status != DnsResponseCode.NoError) {
                            throw new DnsClientException($"Zone transfer failed with {response.Status}", response);
                        }

                        var answers = response.Answers;
                        if (answers == null) {
                            continue;
                        }
                        if (response.WireAnswers.Length != answers.Length) {
                            throw new DnsClientException("Zone transfer wire records did not align with projected answers.");
                        }

                        for (int answerIndex = 0; answerIndex < answers.Length; answerIndex++) {
                            DnsAnswer rec = answers[answerIndex];
                            if (!DnsWireNameCodec.IsSubdomainOrEqual(rec.Name, expectedZone)) {
                                throw new DnsClientException(
                                    $"Zone transfer returned owner '{rec.Name}' outside the requested zone '{expectedZone}'.");
                            }
                            ZoneCanonicalRecord canonicalRecord = ZoneCanonicalRecord.Create(
                                response.WireMessage, response.WireAnswers[answerIndex]);
                            recordCount++;
                            if (recordCount > configuration.MaxZoneTransferRecords) {
                                throw new DnsClientException($"Zone transfer exceeded the configured {configuration.MaxZoneTransferRecords} record limit.");
                            }
                            if (soaCount >= 2) {
                                throw new DnsClientException("Zone transfer incomplete: closing SOA record not last.");
                            }

                            if (!started) {
                                if (rec.Type != DnsRecordType.SOA) {
                                    throw new DnsClientException("Zone transfer did not begin with an SOA record.");
                                }
                                if (!string.Equals(DnsWireNameCodec.Canonical(rec.Name), DnsWireNameCodec.Canonical(expectedZone), StringComparison.Ordinal)) {
                                    throw new DnsClientException("Zone transfer opening SOA owner does not match the requested zone.");
                                }
                                started = true;
                                openingSoa = rec;
                            }

                            if (current.Count == 0 ||
                                (string.Equals(DnsWireNameCodec.Canonical(current[0].Name),
                                    DnsWireNameCodec.Canonical(rec.Name), StringComparison.Ordinal)
                                 && current[0].Type == rec.Type)) {
                                current.Add(rec);
                                currentCanonical.Add(canonicalRecord);
                            } else {
                                bool opening = index == 0 && current[0].Type == DnsRecordType.SOA;
                                bool closing = sawClosing && current[0].Type == DnsRecordType.SOA;
                                yield return new ZoneTransferResult(current.ToArray(), currentCanonical.ToArray(), opening, closing, index++);
                                current.Clear();
                                currentCanonical.Clear();
                                current.Add(rec);
                                currentCanonical.Add(canonicalRecord);
                            }

                            lastRecord = rec;
                            if (rec.Type == DnsRecordType.SOA) {
                                soaCount++;
                                if (soaCount == 2) {
                                    if (!openingSoa.HasValue ||
                                        !string.Equals(DnsWireNameCodec.Canonical(rec.Name), DnsWireNameCodec.Canonical(openingSoa.Value.Name), StringComparison.Ordinal) ||
                                        !string.Equals(rec.DataRaw, openingSoa.Value.DataRaw, StringComparison.OrdinalIgnoreCase)) {
                                        throw new DnsClientException("Zone transfer closing SOA does not match the opening SOA.");
                                    }
                                    sawClosing = true;
                                    if (answerIndex != answers.Length - 1) {
                                        throw new DnsClientException("Zone transfer incomplete: closing SOA record not last.");
                                    }
                                }
                            }
                        }

                        if (sawClosing) {
                            RequireSignedTransferEnd(configuration, frame);
                            if (current.Count > 0) {
                                yield return new ZoneTransferResult(current.ToArray(), currentCanonical.ToArray(), index == 0, true, index);
                            }
                            yield break;
                        }
                    }

                    if (current.Count > 0 && started) {
                        bool opening = index == 0 && current[0].Type == DnsRecordType.SOA;
                        bool closing = sawClosing && current[0].Type == DnsRecordType.SOA;
                        yield return new ZoneTransferResult(current.ToArray(), currentCanonical.ToArray(), opening, closing, index++);
                    }

                    if (soaCount == 0) {
                        throw new DnsClientException("Zone transfer returned no opening SOA record.");
                    }

                    if (soaCount < 2 || lastRecord == null || lastRecord.Value.Type != DnsRecordType.SOA) {
                        throw new DnsClientException("Zone transfer incomplete: closing SOA record missing.");
                    }
        }

        private static async Task ReadExactWithTimeoutAsync(Stream stream, byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken cancellationToken) {
            var readTask = DnsWire.ReadExactAsync(stream, buffer, offset, count, cancellationToken);
            var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
            if (await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false) == timeoutTask) {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Reading from stream timed out after {timeoutMilliseconds} milliseconds.");
            }
            await readTask.ConfigureAwait(false);
        }

        private static void ValidateZoneTransferConfiguration(Configuration configuration) {
            if (configuration.RequestFormat != DnsRequestFormat.DnsOverTCP
                && configuration.RequestFormat != DnsRequestFormat.DnsOverTLS) {
                throw new NotSupportedException(
                    $"Zone transfers require DnsOverTCP or DnsOverTLS, not {configuration.RequestFormat}.");
            }
            if (configuration.MaxZoneTransferMessages <= 0) throw new ArgumentOutOfRangeException(nameof(configuration.MaxZoneTransferMessages));
            if (configuration.MaxZoneTransferRecords <= 0) throw new ArgumentOutOfRangeException(nameof(configuration.MaxZoneTransferRecords));
            if (configuration.MaxZoneTransferBytes <= 0) throw new ArgumentOutOfRangeException(nameof(configuration.MaxZoneTransferBytes));

        }

    }
}
