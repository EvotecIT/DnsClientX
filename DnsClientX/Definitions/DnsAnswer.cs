using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
        /// record without trimming whitespace or changing payload line endings.
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
            ? DnsPresentationFormat.TxtStrings(DataRaw, decoded: true)
            : DataStrings;

        /// <summary>
        /// Converts the raw data to multiple strings. By default, DNS records are stored as a single string.
        /// Some records (mainly TXT) can be split into multiple strings and maximum length of a string is 255 characters.
        /// This method tries to preserve the original format of the data in case user needs to check for that format.
        /// </summary>
        /// <returns>Array of strings representing record data.</returns>
        private string[] ConvertToMultiString() => Type is DnsRecordType.TXT or DnsRecordType.SPF
            ? DnsPresentationFormat.TxtStrings(DataRaw, decoded: false)
            : DataRaw == null ? Array.Empty<string>() : new[] { DataRaw };

        /// <summary>
        /// Converts the data to a string trying to unify the format of the data between different providers
        /// </summary>
        /// <returns>Record data converted to a unified string format.</returns>
        private string ConvertData() {
            if (DataRaw is null) {
                return string.Empty;
            }

            return Type switch {
                DnsRecordType.TXT or DnsRecordType.SPF => DnsPresentationFormat.ConcatenateTxt(DataRaw),
                DnsRecordType.CAA => ConvertCaaRecord(),
                DnsRecordType.DNSKEY => ConvertDnsKeyRecord(),
                DnsRecordType.DS => ConvertDsRecord(),
                DnsRecordType.LOC => ConvertLocRecord(),
                DnsRecordType.NSEC => ConvertNsecRecord(),
                DnsRecordType.TLSA => ConvertTlsaRecord(),
                DnsRecordType.PTR => ConvertPtrRecord(),
                DnsRecordType.NAPTR => ConvertNaptrRecord(),
                DnsRecordType.AAAA => IPAddress.TryParse(DataRaw, out var address) ? address.ToString() : DataRaw,
                DnsRecordType.SVCB or DnsRecordType.HTTPS => DataRaw,
                DnsRecordType.NS or DnsRecordType.CNAME or DnsRecordType.DNAME or
                DnsRecordType.MB or DnsRecordType.MD or DnsRecordType.MF or DnsRecordType.MG or DnsRecordType.MR or
                DnsRecordType.MX or DnsRecordType.AFSDB or DnsRecordType.RT or DnsRecordType.KX or
                DnsRecordType.SOA or DnsRecordType.SRV or DnsRecordType.MINFO or DnsRecordType.RP => DataRaw.ToLowerInvariant(),
                _ => DataRaw
            };
        }

        private string ConvertCaaRecord() {
            // This is a CAA record. Cloudflare returns the data in HEX, so we need to convert it to text.
            // Other providers don't do this.
            if (DataRaw.StartsWith("\\#", StringComparison.Ordinal)) {
                var parts = DataRaw.Split(' ')
                    .Where(part => !string.IsNullOrEmpty(part))
                    .Select(part => part.Trim())
                    .Where(part => Regex.IsMatch(part, @"\A\b[0-9a-fA-F]+\b\Z", RegexOptions.CultureInvariant))
                    .Select(part => Convert.ToByte(part, 16))
                    .ToArray();

                // Get the tag length from the third byte
                int tagLength = parts[2];
                // Get the tag
                var tag = Encoding.UTF8.GetString(parts.Skip(3).Take(tagLength).ToArray());
                // Get the value
                var valueBytes = parts.Skip(3 + tagLength).ToArray();
                var value = Encoding.UTF8.GetString(valueBytes);

                return $"0 {tag} \"{value}\"";
            }

            return DataRaw;
        }

        private string ConvertDnsKeyRecord() {
            // For DNSKEY records, decode the flags, protocol, algorithm, and public key from the record data
            // Depending on the provider, the data may be in HEX or in text
            // can be: 256 3 ECDSAP256SHA256 oJMRESz5E4gYzS/q6XDrvU1qMPYIjCWzJaOau8XNEZeqCYKD5ar0IRd8KqXXFJkqmVfRvMGPmM1x8fGAa2XhSA==
            // can be: 257 3 13 mdsswUyr3DPW132mOi8V9xESWE8jTo0dxCjjnopKl+GqJxpVXckHAeF+KkxLbxILfDLUT0rAK9iUzy1L53eKGQ==
            var parts = DataRaw.Split(' ');
            if (parts.Length >= 4 && Enum.TryParse<DnsKeyAlgorithm>(parts[2], out var algorithm)) {
                return $"{parts[0]} {parts[1]} {algorithm} {parts[3]}";
            }

            return DataRaw;
        }

        private string ConvertDsRecord() {
            // For DS records, decode the key tag, algorithm, digest type and digest
            var parts = DataRaw.Split(' ');
            if (parts.Length >= 4 &&
                ushort.TryParse(parts[0], out var keyTag) &&
                byte.TryParse(parts[1], out var algVal) &&
                byte.TryParse(parts[2], out var digestType)) {
                string algorithmName = Enum.IsDefined(typeof(DnsKeyAlgorithm), (int)algVal)
                    ? ((DnsKeyAlgorithm)algVal).ToString()
                    : parts[1];
                return $"{keyTag} {algorithmName} {digestType} {parts[3]}";
            }

            return DataRaw;
        }

        private string ConvertLocRecord() {
            try {
                byte[] rdata = Convert.FromBase64String(DataRaw);
                return DnsWireRecordFormatter.Format(rdata, DnsRecordType.LOC, 0, (ushort)rdata.Length);
            } catch (FormatException) {
                return DataRaw;
            }
        }

        private string ConvertNsecRecord() {
            // This is a NSEC record. Some providers may return non-standard (google) types.
            // Check if the type is a non-standard type
            var parts = DataRaw.Split(' ');
            string updated = DataRaw;

            foreach (var part in parts) {
                if (part.StartsWith("TYPE", StringComparison.Ordinal)) {
                    // This is a non-standard type. Try to convert it to a standard type.
                    if (Enum.TryParse<DnsRecordType>(part.Substring(4), out var standardType)) {
                        // The conversion was successful. Replace the non-standard type with the standard type.
                        if (!string.IsNullOrEmpty(updated)) {
                            updated = updated.Replace(part, standardType.ToString());
                        }
                    }
                }
            }

            if (!ReferenceEquals(updated, DataRaw)) {
                DataRaw = updated;
            }

            return updated;
        }

        private string ConvertTlsaRecord() {
            // This is a TLSA record. The data is in HEX.
            // The data is in the format: 3 1 1 2b6e0f
            // The first byte is the certificate usage, the second byte is the selector, the third byte is the matching type, and the rest is the certificate association data
            byte[] parts;
            if (DataRaw.StartsWith("\\#", StringComparison.Ordinal)) {
                // Handle hexadecimal format
                parts = DataRaw.Split(' ')
                    .Skip(2) // Skip the first two parts
                    .Where(part => !string.IsNullOrEmpty(part))
                    .Select(part => part.Trim())
                    .Where(part => Regex.IsMatch(part, @"\A\b[0-9a-fA-F]+\b\Z", RegexOptions.CultureInvariant))
                    .Select(part => Convert.ToByte(part, 16)) // Convert from hexadecimal to byte
                    .ToArray();
            } else if (Regex.IsMatch(DataRaw, @"^\d+ \d+ \d+ [\da-fA-F]+$", RegexOptions.CultureInvariant)) {
                // If the DataRaw string is already in the correct format, return it as it is
                return DataRaw;
            } else {
                // Handle Base64 format
                if (string.IsNullOrEmpty(DataRaw)) {
                    return DataRaw;
                }
                parts = Convert.FromBase64String(DataRaw);
            }

            // Get the certificate usage
            var certificateUsage = parts[0];
            // Get the selector
            var selector = parts[1];
            // Get the matching type
            var matchingType = parts[2];
            // Get the certificate association data
            var certificateAssociationData = string.Join("", parts.Skip(3).Select(part => part.ToString("x2")));
            //Console.WriteLine($"{certificateUsage} {selector} {matchingType} {certificateAssociationData}");
            return $"{certificateUsage} {selector} {matchingType} {certificateAssociationData}";
        }

        private string ConvertPtrRecord() {
            // A provider's Base64 form is accepted only when it decodes to a complete DNS name.
            // Ordinary presentation names such as "mail" may also be valid Base64 text.
            try {
                if (!string.IsNullOrEmpty(DataRaw)) {
                    byte[] bytes = Convert.FromBase64String(DataRaw);
                    if (IsCompleteWireName(bytes)) {
                        return FormatPtrWireName(bytes);
                    }
                }
            } catch (FormatException) { }
            // Retain the legacy length-prefixed string form only when it is a complete wire name.
            if (DataRaw.IndexOf('\0') >= 0) {
                byte[] bytes = Encoding.UTF8.GetBytes(DataRaw);
                if (IsCompleteWireName(bytes)) return FormatPtrWireName(bytes);
            }
            return DnsWireNameCodec.TrimTrailingRootDot(DataRaw).ToLowerInvariant();
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

        private string ConvertNaptrRecord() {
            // NAPTR record (RFC 3403)
            // Handles Base64, Hex, or Plain Text DataRaw
            try {
                if (DataRaw.StartsWith("\\#", StringComparison.Ordinal)) {
                    // Hex Encoded (e.g., \# XX XX ...)
                    byte[] rdataHex = DataRaw.Split(' ')
                        .Skip(2) // Skip the "\\#" and the length byte
                        .Where(part => !string.IsNullOrEmpty(part))
                        .Select(part => part.Trim())
                        .Where(part => Regex.IsMatch(part, @"\A\b[0-9a-fA-F]{1,2}\b\Z", RegexOptions.CultureInvariant)) // Match 1 or 2 hex chars
                        .Select(part => Convert.ToByte(part, 16))
                        .ToArray();
                    if (rdataHex.Length > 4) { // Basic validation for minimum RDATA length
                        return ParseNaptrRDataAndFormat(rdataHex);
                    }
                }
            } catch (Exception ex) {
                Settings.Logger.WriteDebug($"Error parsing NAPTR record from Hex: {ex.Message} for DataRaw: {DataRaw}");
                // Fall through to try other formats or return DataRaw at the end
            }

            try {
                // Attempt Base64 Decoding
                if (!string.IsNullOrEmpty(DataRaw)) {
                    byte[] rdataBase64 = Convert.FromBase64String(DataRaw);
                    return ParseNaptrRDataAndFormat(rdataBase64);
                }
            } catch (FormatException) {
                // Not Base64, try parsing as plain text
            } catch (Exception ex) {
                Settings.Logger.WriteDebug($"Error parsing NAPTR record from Base64: {ex.Message} for DataRaw: {DataRaw}");
                // Fall through or return DataRaw at the end
            }

            var tokens = DnsPresentationFormat.Tokenize(DataRaw, out bool complete);
            if (complete && (tokens.Count == 5 || tokens.Count == 6)
                && ushort.TryParse(tokens[0].Value, out ushort order)
                && ushort.TryParse(tokens[1].Value, out ushort preference)) {
                string flags = DnsPresentationFormat.Unescape(tokens[2].Value);
                string service = DnsPresentationFormat.Unescape(tokens[3].Value);
                string regexp = tokens.Count == 6 ? DnsPresentationFormat.Unescape(tokens[4].Value) : string.Empty;
                string replacement = tokens[tokens.Count - 1].Value;
                replacement = DnsWireNameCodec.TrimTrailingRootDot(replacement).ToLowerInvariant();
                return $"{order} {preference} {DnsPresentationFormat.Quote(flags)} {DnsPresentationFormat.Quote(service)} {DnsPresentationFormat.Quote(regexp)} {replacement}";
            }

            // If all parsing attempts fail or if it's an unrecognized format for NAPTR that didn't cleanly parse
            Settings.Logger.WriteDebug($"NAPTR DataRaw '{DataRaw}' did not match known Hex, Base64, or plain text patterns, or failed parsing.");
            return DataRaw; // Fallback
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
                + DnsWireNameCodec.TrimTrailingRootDot(formatted.Substring(replacementStart)).ToLowerInvariant();
        }

    }
}
