using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;

namespace DnsClientX {
    /// <summary>
    /// Represents a DNS resource record returned by the server.
    /// See <a href="https://www.rfc-editor.org/rfc/rfc1035">RFC 1035</a>
    /// for the resource record format.
    /// </summary>
    public struct DnsAnswer {
        private string _name;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsAnswer"/> struct.
        /// </summary>
        public DnsAnswer() {
            _name = string.Empty;
            OriginalName = string.Empty;
            Type = DnsRecordType.A;
            TTL = 0;
            DataRaw = string.Empty;
        }
        /// <summary>
        /// This is the name of the record.
        /// Retains original name as returned by the server.
        /// </summary>
        [JsonIgnore]
        public string OriginalName;

        /// <summary>
        /// This is the name of the record.
        /// Removes the trailing dot if it exists to make it easier to compare with other records across providers.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name {
            get => _name;
            set {
                OriginalName = value;
                if (string.IsNullOrEmpty(value)) {
                    _name = value;
                } else {
                    _name = DnsWireNameCodec.TrimTrailingRootDot(value);
                }
            }
        }

        /// <summary>
        /// The type of DNS record.
        /// </summary>
        [JsonPropertyName("type")]
        public DnsRecordType Type { get; set; }

        /// <summary>
        /// The number of seconds for TTL (time to live) for the record.
        /// </summary>
        [JsonPropertyName("TTL")]
        public int TTL { get; set; }

        /// <summary>
        /// The raw value of the DNS record for the given name and type as received from the server.
        /// The data will be in text for standardized record types and in HEX for unknown types.
        /// </summary>
        [JsonPropertyName("data")]
        public string DataRaw { get; set; }

        /// <summary>
        /// Gets decoded record data. TXT character-strings are concatenated within one resource
        /// record without trimming whitespace or changing payload line endings. Recognized DNS name
        /// fields are lowercase without a trailing root dot; the root itself remains a single dot.
        /// Structured field separators and encoded payload whitespace are normalized without changing DataRaw.
        /// </summary>
        [JsonIgnore]
        public string Data => ConvertData();

        /// <summary>
        /// The value of the DNS record for the given name and type, split into multiple strings if necessary.
        /// Tries to preserve the original format of the data.
        /// </summary>
        [JsonIgnore]
        public string[] DataStrings => ConvertToMultiString();

        /// <summary>
        /// Returns TXT data flattened into a single string for script-friendly output.
        /// Non-TXT records return <see cref="Data"/>.
        /// </summary>
        [JsonIgnore]
        public string TxtConcatenatedData {
            get {
                if (Type != DnsRecordType.TXT) {
                    return Data;
                }

                return NormalizeLineEndings(Data).Replace("\r", string.Empty).Replace("\n", string.Empty);
            }
        }

        /// <summary>
        /// Returns the record parsed into a typed representation when supported.
        /// </summary>
        [JsonIgnore]
        public object? TypedRecord => DnsRecordFactory.Create(this);

        /// <summary>
        /// Gets decoded TXT/SPF character-strings without presentation quotes. Empty strings,
        /// whitespace, and escaped payload octets are preserved. Other types return DataStrings.
        /// </summary>
        [JsonIgnore]
        public string[] DataStringsEscaped => Type is DnsRecordType.TXT or DnsRecordType.SPF
            ? DnsRecordDataPresentation.TxtStrings(DataRaw, Type, decoded: true)
            : DataStrings;

        /// <summary>
        /// Converts the raw data to multiple strings. By default, DNS records are stored as a single string.
        /// Some records (mainly TXT) can be split into multiple strings and maximum length of a string is 255 characters.
        /// This method tries to preserve the original format of the data in case user needs to check for that format.
        /// </summary>
        /// <returns>Array of strings representing record data.</returns>
        private string[] ConvertToMultiString() => Type is DnsRecordType.TXT or DnsRecordType.SPF
            ? DnsRecordDataPresentation.TxtStrings(DataRaw, Type, decoded: false)
            : DataRaw == null ? Array.Empty<string>() : new[] { DataRaw };

        /// <summary>
        /// Converts the data to a string trying to unify the format of the data between different providers
        /// </summary>
        /// <returns>Record data converted to a unified string format.</returns>
        private string ConvertData() {
            if (DataRaw is null) {
                return string.Empty;
            }

            string data = DnsRecordDataPresentation.ExpandGeneric(DataRaw, Type);
            if (data.TrimStart().StartsWith("\\#", StringComparison.Ordinal)) return data;
            return Type switch {
                DnsRecordType.TXT or DnsRecordType.SPF => DnsPresentationFormat.ConcatenateTxt(data),
                DnsRecordType.CAA => DnsRecordDataPresentation.Caa(data),
                DnsRecordType.DNSKEY or DnsRecordType.CDNSKEY or DnsRecordType.DS or DnsRecordType.CDS or
                    DnsRecordType.DLV or DnsRecordType.TA => DnsRecordDataPresentation.Encoded(data, Type),
                DnsRecordType.LOC => ConvertLocRecord(data),
                DnsRecordType.NSEC => DnsRecordDataPresentation.Nsec(data),
                DnsRecordType.TLSA or DnsRecordType.SMIMEA => ConvertTlsaRecord(data),
                DnsRecordType.SSHFP => DnsRecordDataPresentation.Sshfp(data),
                DnsRecordType.PTR => ConvertPtrRecord(data),
                DnsRecordType.NAPTR => ConvertNaptrRecord(data),
                DnsRecordType.A or DnsRecordType.AAAA => IPAddress.TryParse(data, out var address) ? address.ToString() : data,
                DnsRecordType.NS or DnsRecordType.CNAME or DnsRecordType.DNAME or
                    DnsRecordType.MB or DnsRecordType.MD or DnsRecordType.MF or DnsRecordType.MG or DnsRecordType.MR
                    => DnsRecordDataPresentation.Names(data, 1, 0),
                DnsRecordType.MX or DnsRecordType.AFSDB or DnsRecordType.RT or DnsRecordType.KX
                    => DnsRecordDataPresentation.Names(data, 2, 1),
                DnsRecordType.SOA => DnsRecordDataPresentation.Names(data, 7, 0, 1),
                DnsRecordType.SRV => DnsRecordDataPresentation.Names(data, 4, 3),
                DnsRecordType.MINFO or DnsRecordType.RP => DnsRecordDataPresentation.Names(data, 2, 0, 1),
                _ => data
            };
        }

        private static string ConvertLocRecord(string data) {
            if (DnsLocPresentation.TryParse(data, out var loc)) return DnsLocPresentation.Format(loc!);
            try {
                byte[] rdata = Convert.FromBase64String(data);
                if (rdata.Length != 16) return data;
                return DnsWireRecordFormatter.Format(rdata, DnsRecordType.LOC, 0, (ushort)rdata.Length);
            } catch (FormatException) { return data; }
            catch (DnsClientException) { return data; }
        }

        private string ConvertTlsaRecord(string data) {
            string[] fields = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length >= 4 && byte.TryParse(fields[0], out _) && byte.TryParse(fields[1], out _) && byte.TryParse(fields[2], out _)) {
                return DnsRecordDataPresentation.Encoded(data, Type);
            }
            try {
                byte[] rdata = Convert.FromBase64String(data);
                if (rdata.Length > ushort.MaxValue) return data;
                return DnsWireRecordFormatter.Format(rdata, Type, 0, (ushort)rdata.Length);
            } catch (FormatException) { return data; }
            catch (DnsClientException) { return data; }
        }

        private string ConvertPtrRecord(string data) {
            // A provider's Base64 form is accepted only when it decodes to a complete DNS name.
            // Ordinary presentation names such as "mail" may also be valid Base64 text.
            try {
                if (!string.IsNullOrEmpty(data)) {
                    byte[] bytes = Convert.FromBase64String(data);
                    if (IsCompleteWireName(bytes)) {
                        return FormatPtrWireName(bytes);
                    }
                }
            } catch (FormatException) { }
            // Retain the legacy length-prefixed string form only when it is a complete wire name.
            if (data.IndexOf('\0') >= 0) {
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                if (IsCompleteWireName(bytes)) return FormatPtrWireName(bytes);
            }
            return DnsRecordDataPresentation.NormalizeName(data);
        }

        private static string FormatPtrWireName(byte[] bytes) => DnsWireNameCodec.TrimTrailingRootDot(
            DnsWireRecordFormatter.Format(bytes, DnsRecordType.PTR, 0, checked((ushort)bytes.Length))).ToLowerInvariant();

        private static bool IsCompleteWireName(byte[] bytes) {
            if (bytes.Length == 0 || bytes.Length > 255) return false;
            int index = 0;
            while (index < bytes.Length) {
                int length = bytes[index++];
                if (length == 0) return index == bytes.Length;
                if (length > 63 || index + length >= bytes.Length) return false;
                index += length;
            }
            return false;
        }

        private string ConvertNaptrRecord(string data) {
            // NAPTR record (RFC 3403)
            // Handles Base64, Hex, or Plain Text data
            try {
                if (data.StartsWith("\\#", StringComparison.Ordinal)) {
                    if (!DnsPresentationFormat.TryDecodeRfc3597(data, out byte[] rdataHex)) return data;
                    return ParseNaptrRDataAndFormat(rdataHex);
                }
            } catch (Exception ex) {
                Settings.Logger.WriteDebug($"Error parsing NAPTR record from Hex: {ex.Message} for data: {data}");
                // Fall through to try other formats or return data at the end
            }

            try {
                // Attempt Base64 Decoding
                if (!string.IsNullOrEmpty(data)) {
                    byte[] rdataBase64 = Convert.FromBase64String(data);
                    return ParseNaptrRDataAndFormat(rdataBase64);
                }
            } catch (FormatException) {
                // Not Base64, try parsing as plain text
            } catch (Exception ex) {
                Settings.Logger.WriteDebug($"Error parsing NAPTR record from Base64: {ex.Message} for data: {data}");
                // Fall through or return data at the end
            }

            var tokens = DnsPresentationFormat.Tokenize(data, out bool complete);
            if (complete && (tokens.Count == 5 || tokens.Count == 6)
                && ushort.TryParse(tokens[0].Value, out ushort order)
                && ushort.TryParse(tokens[1].Value, out ushort preference)) {
                string flags = DnsPresentationFormat.Unescape(tokens[2].Value);
                string service = DnsPresentationFormat.Unescape(tokens[3].Value);
                string regexp = tokens.Count == 6 ? DnsPresentationFormat.Unescape(tokens[4].Value) : string.Empty;
                string replacement = tokens[tokens.Count - 1].Value;
                replacement = DnsRecordDataPresentation.NormalizeName(replacement);
                return $"{order} {preference} {DnsPresentationFormat.Quote(flags)} {DnsPresentationFormat.Quote(service)} {DnsPresentationFormat.Quote(regexp)} {replacement}";
            }

            // If all parsing attempts fail or if it's an unrecognized format for NAPTR that didn't cleanly parse
            Settings.Logger.WriteDebug($"NAPTR data '{data}' did not match known Hex, Base64, or plain text patterns, or failed parsing.");
            return data; // Fallback
        }

        /// <summary>
        /// Normalizes line endings to '\n' regardless of the original platform.
        /// </summary>
        /// <param name="data">Input string</param>
        /// <returns>String with '\n' line endings</returns>
        private static string NormalizeLineEndings(string data) {
            if (string.IsNullOrEmpty(data)) return string.Empty;

            return data.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>
        /// Formats binary NAPTR data with faithful character-string escaping and a normalized replacement.
        /// </summary>
        /// <param name="rdata">The raw data in special format.</param>
        /// <returns>The data in standard dotted format.</returns>
        private string ParseNaptrRDataAndFormat(byte[] rdata) {
            string formatted = DnsWireRecordFormatter.Format(rdata, DnsRecordType.NAPTR, 0, checked((ushort)rdata.Length));
            int replacementStart = formatted.LastIndexOf(' ') + 1;
            return formatted.Substring(0, replacementStart)
                + DnsRecordDataPresentation.NormalizeName(formatted.Substring(replacementStart));
        }

    }
}
