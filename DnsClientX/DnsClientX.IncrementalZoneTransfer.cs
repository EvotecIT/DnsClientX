using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        /// <summary>Requests an RFC 1995 incremental zone transfer using the caller's current SOA.</summary>
        /// <remarks>The result is returned only after the entire response sequence is validated.</remarks>
        public async Task<IncrementalZoneTransferResult> IncrementalZoneTransferAsync(
            string zone,
            SoaRecord currentSoa,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(zone)) throw new ArgumentNullException(nameof(zone));
            if (currentSoa == null) throw new ArgumentNullException(nameof(currentSoa));
            Configuration configuration = EndpointConfiguration.CreateQuerySnapshot();
            ValidateZoneTransferConfiguration(configuration);
            var query = DnsIxfrQueryMessage.Create(zone, currentSoa, configuration.CheckingDisabled);
            List<DnsAnswer> records = await ReceiveIxfrAsync(
                    query, zone, currentSoa.Serial, configuration, cancellationToken)
                .ConfigureAwait(false);
            return ParseIxfr(zone, currentSoa.Serial, records);
        }

        /// <summary>Requests an RFC 1995 incremental transfer using a current serial number.</summary>
        /// <remarks>The generated authority SOA uses root names and zero timing fields because only its serial is significant to IXFR.</remarks>
        public Task<IncrementalZoneTransferResult> IncrementalZoneTransferAsync(
            string zone,
            uint currentSerial,
            CancellationToken cancellationToken = default) {
            return IncrementalZoneTransferAsync(
                zone,
                new SoaRecord(".", ".", currentSerial, 0, 0, 0, 0),
                cancellationToken);
        }

        private async Task<List<DnsAnswer>> ReceiveIxfrAsync(DnsIxfrQueryMessage query, string zone,
            uint requestedSerial, Configuration configuration, CancellationToken cancellationToken) {
            await using DnsZoneTransferConnection connection = await DnsZoneTransferConnection.ConnectAsync(
                configuration.Hostname!, configuration.Port, configuration.TimeOut, configuration,
                IgnoreCertificateErrors, cancellationToken).ConfigureAwait(false);
            Stream stream = connection.Stream;
            await WriteFramedTransferQueryAsync(stream, query.WireData, configuration.TimeOut,
                configuration.Hostname!, configuration.Port, cancellationToken).ConfigureAwait(false);

            var records = new List<DnsAnswer>();
            var lengthBuffer = new byte[2];
            DnsAnswer? openingSoa = null;
            int messageCount = 0;
            long transferredBytes = 0;
            while (true) {
                await ReadExactWithTimeoutAsync(stream, lengthBuffer, 0, 2, configuration.TimeOut, cancellationToken)
                    .ConfigureAwait(false);
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBuffer);
                int length = BitConverter.ToUInt16(lengthBuffer, 0);
                messageCount++;
                transferredBytes += length + 2L;
                if (messageCount > configuration.MaxZoneTransferMessages) {
                    throw new DnsClientException($"Incremental zone transfer exceeded the configured {configuration.MaxZoneTransferMessages} message limit.");
                }
                if (transferredBytes > configuration.MaxZoneTransferBytes) {
                    throw new DnsClientException($"Incremental zone transfer exceeded the configured {configuration.MaxZoneTransferBytes} byte limit.");
                }

                var responseBuffer = new byte[length];
                await ReadExactWithTimeoutAsync(stream, responseBuffer, 0, length, configuration.TimeOut, cancellationToken)
                    .ConfigureAwait(false);
                ValidateTransferEnvelope(responseBuffer, query.TransactionId);
                DnsResponse response = await DnsWire.DeserializeDnsWireFormat(null, Debug, responseBuffer)
                    .ConfigureAwait(false);
                response.AddServerDetails(configuration);
                if (response.Status != DnsResponseCode.NoError) {
                    throw new DnsClientException($"Incremental zone transfer failed with {response.Status}.", response);
                }

                DnsAnswer[] answers = response.Answers ?? Array.Empty<DnsAnswer>();
                foreach (DnsAnswer answer in answers) {
                    if (records.Count >= configuration.MaxZoneTransferRecords) {
                        throw new DnsClientException($"Incremental zone transfer exceeded the configured {configuration.MaxZoneTransferRecords} record limit.");
                    }
                    if (openingSoa == null) {
                        if (answer.Type != DnsRecordType.SOA
                            || !SameName(answer.Name, zone)) {
                            throw new DnsClientException("Incremental zone transfer did not begin with the zone SOA.");
                        }
                        openingSoa = answer;
                    }
                    records.Add(answer);
                }

                if (records.Count == 1 && answers.Length == 1
                    && ParseSoa(records[0]).Serial == requestedSerial) {
                    return records;
                }
                if (openingSoa.HasValue && records.Count > 1
                    && answers.Length > 0
                    && answers[answers.Length - 1].Type == DnsRecordType.SOA
                    && SameSoa(answers[answers.Length - 1], openingSoa.Value)) {
                    return records;
                }
            }
        }

        private static IncrementalZoneTransferResult ParseIxfr(string zone, uint requestedSerial,
            IReadOnlyList<DnsAnswer> records) {
            if (records.Count == 0 || records[0].Type != DnsRecordType.SOA || !SameName(records[0].Name, zone)) {
                throw new DnsClientException("Incremental zone transfer is missing the primary's current SOA.");
            }
            SoaRecord current = ParseSoa(records[0]);
            if (records.Count == 1) {
                if (current.Serial != requestedSerial) {
                    throw new DnsClientException(
                        $"A single-SOA IXFR response reported serial {current.Serial}, not the requested serial {requestedSerial}.");
                }
                return new IncrementalZoneTransferResult(IncrementalZoneTransferKind.NoChange, current);
            }
            if (records[records.Count - 1].Type != DnsRecordType.SOA
                || !SameSoa(records[0], records[records.Count - 1])) {
                throw new DnsClientException("Incremental zone transfer is missing its closing current SOA.");
            }

            if (records[1].Type != DnsRecordType.SOA) {
                return new IncrementalZoneTransferResult(
                    IncrementalZoneTransferKind.FullTransfer,
                    current,
                    fullZoneRecords: records);
            }

            var changes = new List<IncrementalZoneChange>();
            int index = 1;
            uint expectedOldSerial = requestedSerial;
            while (index < records.Count - 1) {
                DnsAnswer previousAnswer = records[index++];
                if (previousAnswer.Type != DnsRecordType.SOA || !SameName(previousAnswer.Name, zone)) {
                    throw new DnsClientException("IXFR delete sequence did not begin with an SOA.");
                }
                SoaRecord previous = ParseSoa(previousAnswer);
                if (previous.Serial != expectedOldSerial) {
                    throw new DnsClientException($"IXFR serial chain expected {expectedOldSerial} but received {previous.Serial}.");
                }

                var deleted = new List<DnsAnswer>();
                while (index < records.Count - 1 && records[index].Type != DnsRecordType.SOA) {
                    deleted.Add(records[index++]);
                }
                if (index >= records.Count - 1) throw new DnsClientException("IXFR delete sequence is missing its new SOA.");

                SoaRecord next = ParseSoa(records[index++]);
                if (!SerialIsNewer(next.Serial, previous.Serial)) {
                    throw new DnsClientException($"IXFR new SOA serial {next.Serial} is not newer than {previous.Serial}.");
                }
                var added = new List<DnsAnswer>();
                while (index < records.Count - 1 && records[index].Type != DnsRecordType.SOA) {
                    added.Add(records[index++]);
                }
                changes.Add(new IncrementalZoneChange(previous, next, deleted, added));
                expectedOldSerial = next.Serial;
            }

            if (changes.Count == 0 || expectedOldSerial != current.Serial) {
                throw new DnsClientException(
                    $"IXFR serial chain ended at {expectedOldSerial}, not the primary's current serial {current.Serial}.");
            }
            return new IncrementalZoneTransferResult(IncrementalZoneTransferKind.Incremental, current, changes);
        }

        private static SoaRecord ParseSoa(DnsAnswer answer) {
            string[] parts = answer.DataRaw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 7
                || !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint serial)
                || !uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint refresh)
                || !uint.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out uint retry)
                || !uint.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out uint expire)
                || !uint.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out uint minimum)) {
                throw new DnsClientException("Zone transfer returned malformed SOA presentation data.");
            }
            return new SoaRecord(parts[0], parts[1], serial, refresh, retry, expire, minimum);
        }

        private static bool SameName(string left, string right) =>
            string.Equals(DnsWireNameCodec.Canonical(left), DnsWireNameCodec.Canonical(right), StringComparison.Ordinal);

        private static bool SameSoa(DnsAnswer left, DnsAnswer right) =>
            SameName(left.Name, right.Name)
            && string.Equals(left.DataRaw, right.DataRaw, StringComparison.OrdinalIgnoreCase);

        private static bool SerialIsNewer(uint candidate, uint current) =>
            candidate != current && unchecked((int)(candidate - current)) > 0;

        private static void ValidateTransferEnvelope(byte[] response, ushort expectedTransactionId) {
            if (response.Length < 12) throw new DnsClientException("Zone transfer returned a truncated DNS header.");
            ushort responseId = (ushort)((response[0] << 8) | response[1]);
            ushort flags = (ushort)((response[2] << 8) | response[3]);
            if (responseId != expectedTransactionId) throw new DnsClientException("Zone transfer response transaction ID did not match the request.");
            if ((flags & 0x8000) == 0 || ((flags >> 11) & 0x0F) != 0) {
                throw new DnsClientException("Zone transfer returned an invalid DNS response envelope.");
            }
        }

        private static async Task WriteFramedTransferQueryAsync(Stream stream, byte[] query, int timeoutMilliseconds,
            string dnsServer, int port, CancellationToken cancellationToken) {
            byte[] length = { (byte)(query.Length >> 8), (byte)query.Length };
            await WriteWithTimeoutAsync(stream, length, timeoutMilliseconds, dnsServer, port, cancellationToken)
                .ConfigureAwait(false);
            await WriteWithTimeoutAsync(stream, query, timeoutMilliseconds, dnsServer, port, cancellationToken)
                .ConfigureAwait(false);
        }

        private static async Task WriteWithTimeoutAsync(Stream stream, byte[] value, int timeoutMilliseconds,
            string dnsServer, int port, CancellationToken cancellationToken) {
            Task write = stream.WriteAsync(value, 0, value.Length, cancellationToken);
            if (await Task.WhenAny(write, Task.Delay(timeoutMilliseconds, cancellationToken)).ConfigureAwait(false) != write) {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Writing to {dnsServer}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
            await write.ConfigureAwait(false);
        }
    }
}
