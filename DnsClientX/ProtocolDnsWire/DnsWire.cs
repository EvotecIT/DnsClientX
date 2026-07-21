using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Provides DNS wire-format serialization and response parsing.
    /// </summary>
    internal static class DnsWire {
        internal static Task<DnsResponse> DeserializeDnsWireFormat(this HttpResponseMessage? res,
            bool debug = false, byte[]? bytes = null) =>
            DeserializeDnsWireCore(res, debug, bytes, query: null, requireResponse: false);

        internal static Task<DnsResponse> DeserializeDnsWireResponse(this HttpResponseMessage? res,
            bool debug, byte[]? bytes, DnsMessage query) =>
            DeserializeDnsWireCore(res, debug, bytes, query, requireResponse: true);

        internal static async Task<DnsResponse> DeserializeDnsUpdateResponse(byte[] bytes, bool debug,
            ushort transactionId, string zone) {
            DnsResponse response = await DeserializeDnsWireCore(null, debug, bytes, query: null, requireResponse: true).ConfigureAwait(false);
            if (response.TransactionId != transactionId) throw new DnsClientException("DNS UPDATE response transaction ID does not match the request.");
            if (response.OperationCode != 5) throw new DnsClientException($"DNS UPDATE response has unexpected opcode {response.OperationCode}.");
            if (response.Questions == null || response.Questions.Length != 1 ||
                response.Questions[0].Type != DnsRecordType.SOA ||
                !string.Equals(DnsWireNameCodec.Canonical(response.Questions[0].Name), DnsWireNameCodec.Canonical(zone), StringComparison.Ordinal)) {
                throw new DnsClientException("DNS UPDATE response zone section does not match the request.");
            }
            return response;
        }

        private static async Task<DnsResponse> DeserializeDnsWireCore(HttpResponseMessage? res,
            bool debug, byte[]? bytes, DnsMessage? query, bool requireResponse) {
            if (res == null && bytes == null) throw new ArgumentNullException(nameof(res));
            try {
                byte[] message;
                if (bytes != null) {
                    message = bytes;
                } else {
                    HttpResponseMessage response = res ?? throw new ArgumentNullException(nameof(res));
                    using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    if (stream.CanSeek && stream.Length == 0) {
                        throw new DnsClientException("Response content is empty; it cannot be parsed as a DNS message.");
                    }
                    using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer).ConfigureAwait(false);
                    message = buffer.ToArray();
                }

                if (debug) {
                    if (res?.RequestMessage?.RequestUri is Uri requestUri) Settings.Logger.WriteDebug("Response Uri: " + requestUri);
                    Settings.Logger.WriteDebug("DNS response bytes: " + BitConverter.ToString(message));
                }

                return ParseMessage(message, query, requireResponse || query != null);
            } catch (DnsClientException) {
                throw;
            } catch (Exception ex) {
                throw new DnsClientException("Invalid DNS wire response: " + ex.Message, ex);
            }
        }

        // Retained for internal binary/source compatibility with older tests and custom builds.
        // Production response parsing uses the bounded message-aware formatter directly.
        internal static string ProcessRecordData(byte[] dnsMessage, int recordStart, DnsRecordType type,
            byte[] rdata, ushort rdLength, long messageStart) {
            if (rdata == null) throw new ArgumentNullException(nameof(rdata));
            if (rdLength > rdata.Length) throw new DnsClientException("RDATA length exceeds the supplied buffer.");
            return DnsWireRecordFormatter.Format(rdata, type, 0, rdLength);
        }

        private static DnsResponse ParseMessage(byte[] message, DnsMessage? query, bool requireResponse) {
            if (message.Length < 12) throw new DnsClientException("DNS message is shorter than its 12-byte header.");
            if (message.Length > ushort.MaxValue) throw new DnsClientException("DNS message exceeds the 65535-octet protocol limit.");
            var reader = new DnsWireReader(message);
            ushort transactionId = reader.ReadUInt16();
            ushort flags = reader.ReadUInt16();
            ushort questionCount = reader.ReadUInt16();
            ushort answerCount = reader.ReadUInt16();
            ushort authorityCount = reader.ReadUInt16();
            ushort additionalCount = reader.ReadUInt16();

            bool isResponse = (flags & 0x8000) != 0;
            int opcode = (flags >> 11) & 0x0F;
            if (requireResponse && !isResponse) throw new DnsClientException("DNS packet is not a response (QR=0).");
            if (query != null && transactionId != query.TransactionId) {
                throw new DnsClientException($"DNS response transaction ID {transactionId} does not match query ID {query.TransactionId}.");
            }
            if (query != null && opcode != 0) throw new DnsClientException($"DNS response opcode {opcode} does not match a standard query.");

            var questions = new DnsQuestion[questionCount];
            var questionClasses = new ushort[questionCount];
            for (int i = 0; i < questionCount; i++) {
                string name = reader.ReadName();
                DnsRecordType type = (DnsRecordType)reader.ReadUInt16();
                ushort queryClass = reader.ReadUInt16();
                questions[i] = new DnsQuestion { Name = name, Type = type, OriginalName = name };
                questionClasses[i] = queryClass;
            }

            if (query != null) {
                if (questionCount != 1) throw new DnsClientException($"DNS response contains {questionCount} questions; exactly one was expected.");
                if (!string.Equals(DnsWireNameCodec.Canonical(questions[0].Name), DnsWireNameCodec.Canonical(query.Name), StringComparison.Ordinal) ||
                    questions[0].Type != query.Type || questionClasses[0] != query.QueryClass) {
                    throw new DnsClientException("DNS response question does not match the requested name, type, and class.");
                }
            }

            DnsWireResourceRecord[] answerRecords = ReadRecords(reader, answerCount);
            DnsWireResourceRecord[] authorityRecords = ReadRecords(reader, authorityCount);
            DnsWireResourceRecord[] additionalRecords = ReadRecords(reader, additionalCount);
            if (Array.Exists(answerRecords, record => record.Type == DnsRecordType.OPT) ||
                Array.Exists(authorityRecords, record => record.Type == DnsRecordType.OPT)) {
                throw new DnsClientException("The OPT pseudo-record is only valid in the additional section.");
            }
            if (!reader.IsAtEnd) throw new DnsClientException("DNS response contains trailing bytes after the declared sections.");

            int extendedRcode = 0;
            int? ednsPayloadSize = null;
            byte? ednsVersion = null;
            bool ednsDnsSecOk = false;
            byte[] nsid = Array.Empty<byte>();
            byte[] cookie = Array.Empty<byte>();
            string ednsClientSubnet = string.Empty;
            var extendedErrors = new List<ExtendedDnsError>();
            bool sawOpt = false;

            foreach (DnsWireResourceRecord record in additionalRecords) {
                if (record.Type != DnsRecordType.OPT) continue;
                if (sawOpt) throw new DnsClientException("DNS response contains more than one OPT pseudo-record.");
                sawOpt = true;
                if (record.Name != ".") throw new DnsClientException("The OPT pseudo-record owner name must be the root name.");
                ednsPayloadSize = record.Class;
                extendedRcode = (int)((record.RawTtl >> 24) & 0xFF);
                ednsVersion = (byte)((record.RawTtl >> 16) & 0xFF);
                ednsDnsSecOk = (record.RawTtl & 0x8000) != 0;
                ParseEdnsOptions(message, record, extendedErrors, ref nsid, ref cookie, ref ednsClientSubnet);
            }

            int responseCode = (flags & 0x000F) | (extendedRcode << 4);
            var response = new DnsResponse {
                TransactionId = transactionId,
                IsResponse = isResponse,
                OperationCode = opcode,
                Status = (DnsResponseCode)responseCode,
                IsAuthoritativeAnswer = (flags & 0x0400) != 0,
                IsTruncated = (flags & 0x0200) != 0,
                IsRecursionDesired = (flags & 0x0100) != 0,
                IsRecursionAvailable = (flags & 0x0080) != 0,
                AuthenticData = (flags & 0x0020) != 0,
                CheckingDisabled = (flags & 0x0010) != 0,
                Questions = questions,
                Answers = ToAnswers(answerRecords),
                Authorities = ToAnswers(authorityRecords),
                Additional = ToAnswers(additionalRecords),
                ExtendedDnsErrors = extendedErrors.ToArray(),
                EdnsClientSubnet = ednsClientSubnet,
                EdnsUdpPayloadSize = ednsPayloadSize,
                EdnsVersion = ednsVersion,
                EdnsDnsSecOk = ednsDnsSecOk,
                EdnsNsid = nsid,
                EdnsCookie = cookie,
                WireMessage = (byte[])message.Clone(),
                WireAnswers = answerRecords,
                WireAuthorities = authorityRecords,
                WireAdditional = additionalRecords
            };
            response.RefreshDerivedData();
            return response;
        }

        private static DnsWireResourceRecord[] ReadRecords(DnsWireReader reader, ushort count) {
            if (count == 0) return Array.Empty<DnsWireResourceRecord>();
            var records = new DnsWireResourceRecord[count];
            for (int i = 0; i < count; i++) {
                string name = reader.ReadName();
                DnsRecordType type = (DnsRecordType)reader.ReadUInt16();
                ushort recordClass = reader.ReadUInt16();
                uint rawTtl = reader.ReadUInt32();
                ushort length = reader.ReadUInt16();
                int rdataOffset = reader.Position;
                reader.Skip(length);
                int ttl = type == DnsRecordType.OPT ? 0 : rawTtl > int.MaxValue ? int.MaxValue : (int)rawTtl;
                string data = DnsWireRecordFormatter.Format(reader.Message, type, rdataOffset, length);
                records[i] = new DnsWireResourceRecord(name, type, recordClass, ttl, rawTtl, rdataOffset, length, data);
            }
            return records;
        }

        private static DnsAnswer[] ToAnswers(DnsWireResourceRecord[] records) {
            var answers = new DnsAnswer[records.Length];
            for (int i = 0; i < records.Length; i++) {
                answers[i] = new DnsAnswer {
                    Name = records[i].Name,
                    Type = records[i].Type,
                    TTL = records[i].Ttl,
                    DataRaw = records[i].Data
                };
            }
            return answers;
        }

        private static void ParseEdnsOptions(byte[] message, DnsWireResourceRecord record,
            List<ExtendedDnsError> errors, ref byte[] nsid, ref byte[] cookie, ref string subnet) {
            var reader = new DnsWireReader(message, record.RdataOffset, record.RdataOffset + record.RdataLength);
            while (!reader.IsAtEnd) {
                ushort code = reader.ReadUInt16();
                ushort length = reader.ReadUInt16();
                int end = checked(reader.Position + length);
                if (end > reader.End) throw new DnsClientException("EDNS option length exceeds the OPT RDATA boundary.");
                byte[] value = reader.ReadBytes(length);
                switch (code) {
                    case 3: // NSID, RFC 5001
                        nsid = value;
                        break;
                    case 8: // ECS, RFC 7871
                        subnet = DnsWireRecordFormatter.FormatClientSubnet(value);
                        break;
                    case 10: // COOKIE, RFC 7873
                        if (!CookieOption.IsValidResponseLength(value.Length)) {
                            throw new DnsClientException("EDNS Cookie must contain an 8-byte client cookie and either no server cookie or an 8-32 byte server cookie.");
                        }
                        cookie = value;
                        break;
                    case 15: // EDE, RFC 8914
                        if (value.Length < 2) throw new DnsClientException("Extended DNS Error option is shorter than its 2-byte info code.");
                        int infoCode = (value[0] << 8) | value[1];
                        string text = value.Length == 2 ? string.Empty : System.Text.Encoding.UTF8.GetString(value, 2, value.Length - 2);
                        errors.Add(new ExtendedDnsError { InfoCode = infoCode, ExtraText = text });
                        break;
                }
            }
        }

        /// <summary>
        /// Reads exactly the requested number of bytes or throws when the stream ends early.
        /// </summary>
        internal static async Task ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            while (count > 0) {
                int read = await stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("Stream ended before the declared DNS message length.");
                offset += read;
                count -= read;
            }
        }
    }
}
