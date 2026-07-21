using System;
using System.Collections.Generic;
using System.Linq;
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
                    _name = value.EndsWith(".", StringComparison.Ordinal) ? value.TrimEnd('.') : value;
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
        /// the value of the DNS record for the given name and type after being processed and converted to a string.
        /// </summary>
        [JsonIgnore]
        public string Data => NormalizeLineEndings(ConvertData());

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
        /// The value of the DNS record for the given name and type, escaped if necessary removing the quotes completely.
        /// </summary>
        [JsonIgnore]
        public string[] DataStringsEscaped {
            get {
                var data = new List<string>();
                foreach (var item in DataStrings) {
                    if (item.StartsWith("\\\"")) {
                        data.Add(item.Replace("\\\"", string.Empty).Replace("\"", string.Empty));
                    } else {
                        data.Add(item);
                    }
                }
                return data.ToArray();
            }
        }

        /// <summary>
        /// Converts the raw data to multiple strings. By default, DNS records are stored as a single string.
        /// Some records (mainly TXT) can be split into multiple strings and maximum length of a string is 255 characters.
        /// This method tries to preserve the original format of the data in case user needs to check for that format.
        /// </summary>
        /// <returns>Array of strings representing record data.</returns>
        private string[] ConvertToMultiString() {
            string dataToProcess = DataRaw;

            if (dataToProcess is null) {
                return Array.Empty<string>();
            }

            // I'm not sure if this is the best way to do this, but it works for now.
            // This method searches for quotes with space between them or quotes without space
            // Then it splits the string into multiple strings, adding back the quotes that we split on
            var data = new List<string>();
            var temp = new StringBuilder();

            for (int i = 0; i < dataToProcess.Length; i++) {
                if (i < dataToProcess.Length - 1 && dataToProcess[i] == '"' && dataToProcess[i + 1] == '"') {
                    temp.Append(dataToProcess[i]);
                    data.Add(temp.ToString());
                    temp.Clear();
                    temp.Append("\""); // Add quotes back
                    i++; // Skip the next character as it's part of the split
                } else if (i < dataToProcess.Length - 2 && dataToProcess[i] == '"' && dataToProcess[i + 1] == ' ' && dataToProcess[i + 2] == '"') {
                    temp.Append(dataToProcess[i]);
                    data.Add(temp.ToString());
                    temp.Clear();
                    temp.Append("\""); // Add quotes back
                    i += 2; // Skip the next two characters as they're part of the split
                } else {
                    temp.Append(dataToProcess[i]);
                }
            }

            if (temp.Length > 0) {
                data.Add(temp.ToString());
            }

            // Clean up empty strings and whitespace-only entries for TXT records
            if (Type == DnsRecordType.TXT) {
                data = data.Where(s => !string.IsNullOrWhiteSpace(s) && s.Trim('"').Trim().Length > 0).ToList();
            }

            return data.ToArray();
        }

        /// <summary>
        /// Converts the data to a string trying to unify the format of the data between different providers
        /// </summary>
        /// <returns>Record data converted to a unified string format.</returns>
        private string ConvertData() {
            if (DataRaw is null) {
                return string.Empty;
            }

            return Type switch {
                DnsRecordType.TXT => ConvertTxtRecord(),
                DnsRecordType.CAA => ConvertCaaRecord(),
                DnsRecordType.DNSKEY => ConvertDnsKeyRecord(),
                DnsRecordType.DS => ConvertDsRecord(),
                DnsRecordType.LOC => ConvertLocRecord(),
                DnsRecordType.NSEC => ConvertNsecRecord(),
                DnsRecordType.TLSA => ConvertTlsaRecord(),
                DnsRecordType.PTR => ConvertPtrRecord(),
                DnsRecordType.NAPTR => ConvertNaptrRecord(),
                DnsRecordType.SVCB or DnsRecordType.HTTPS => DataRaw,
                _ => DataRaw.ToLowerInvariant()
            };
        }

        private string ConvertTxtRecord() {
            string[] segments = ConvertToMultiString();
            if (segments.Length == 0) return string.Empty;

            var result = new StringBuilder(DataRaw.Length);
            foreach (string segment in segments) {
                string value = segment;
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"') {
                    value = value.Substring(1, value.Length - 2);
                }
                result.Append(UnescapePresentationText(value));
            }
            return CleanupTxtRecordData(result.ToString());
        }

        private static string UnescapePresentationText(string value) {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0) return value;
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++) {
                if (value[i] != '\\' || i + 1 >= value.Length) {
                    builder.Append(value[i]);
                    continue;
                }

                if (i + 3 < value.Length &&
                    value[i + 1] >= '0' && value[i + 1] <= '9' &&
                    value[i + 2] >= '0' && value[i + 2] <= '9' &&
                    value[i + 3] >= '0' && value[i + 3] <= '9') {
                    int octet = (value[i + 1] - '0') * 100 + (value[i + 2] - '0') * 10 + value[i + 3] - '0';
                    if (octet <= byte.MaxValue) {
                        builder.Append((char)octet);
                        i += 3;
                        continue;
                    }
                }

                builder.Append(value[++i]);
            }
            return builder.ToString();
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
            // For PTR records, decode the domain name from the record data
            try {
                // First try to decode as Base64
                if (!string.IsNullOrEmpty(DataRaw)) {
                    var output = Encoding.UTF8.GetString(Convert.FromBase64String(DataRaw));
                    return ConvertSpecialFormatToDotted(output);
                }
            } catch (FormatException) {
                // Ignore and try special format directly
            }

            return ConvertSpecialFormatToDotted(DataRaw);
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

            try {
                // Plain Text Parsing (e.g., Google JSON: 10 100 s SIP+D2T  _sip._tcp.sip2sip.info.)
                // Or already formatted: 10 100 "s" "SIP+D2T" "" _sip._tcp.sip2sip.info.
                var parts = new List<string>();
                var currentPart = new StringBuilder();
                bool inQuotes = false;
                foreach (char c in DataRaw) {
                    if (c == '\"') {
                        inQuotes = !inQuotes;
                        currentPart.Append(c);
                    } else if (c == ' ' && !inQuotes) {
                        if (currentPart.Length > 0) {
                            parts.Add(currentPart.ToString());
                            currentPart.Clear();
                        }
                    } else {
                        currentPart.Append(c);
                    }
                }
                if (currentPart.Length > 0) {
                    parts.Add(currentPart.ToString());
                }

                if (parts.Count >= 5) {
                    string orderStr = parts[0];
                    string preferenceStr = parts[1];
                    string flags = parts[2].Trim('"');
                    string service = parts[3].Trim('"');
                    string regexp;
                    string replacement;

                    if (parts.Count == 5) { // Format like: 10 100 s SIP+D2T _replacement.domain.
                        regexp = string.Empty;
                        replacement = parts[4];
                    } else { // Format like: 10 100 s SIP+D2T "regexp" _replacement.domain. or 10 100 "s" "SIP+D2T" "" target.
                        regexp = parts[4].Trim('"');
                        replacement = string.Join(" ", parts.Skip(5).ToArray()); // Should be a single domain part
                    }

                    // Validate Order and Preference are numbers
                    if (ushort.TryParse(orderStr, out ushort order) && ushort.TryParse(preferenceStr, out ushort preferenceVal)) {
                        string finalReplacement = (replacement == ".") ? "." : replacement.TrimEnd('.');
                        return $"{order} {preferenceVal} \"{flags}\" \"{service}\" \"{regexp}\" {finalReplacement}";
                    }
                }
            } catch (Exception ex) {
                Settings.Logger.WriteDebug($"Error parsing NAPTR record from plain text: {ex.Message} for DataRaw: {DataRaw}");
            }

            // If all parsing attempts fail or if it's an unrecognized format for NAPTR that didn't cleanly parse
            Settings.Logger.WriteDebug($"NAPTR DataRaw '{DataRaw}' did not match known Hex, Base64, or plain text patterns, or failed parsing.");
            return DataRaw; // Fallback
        }

        /// <summary>
        /// Centralized cleanup method for TXT record data to ensure consistency across all DNS providers.
        /// Removes empty lines, trims whitespace, and normalizes line endings.
        /// </summary>
        /// <param name="data">The TXT record data to clean up</param>
        /// <returns>Cleaned TXT record data</returns>
        private string CleanupTxtRecordData(string data) {
            if (string.IsNullOrWhiteSpace(data)) return string.Empty;

            // Split on various line ending combinations and remove empty entries
            var lines = data.Split(new string[] { "\n", "\r", "\r\n", "\n\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim()) // Trim each line
                .Where(line => !string.IsNullOrWhiteSpace(line)) // Remove empty or whitespace-only lines
                .ToArray();

            return string.Join("\n", lines);
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
        /// Converts a special format like "\u0003one\u0003one\u0003one\u0003one\0" to a standard dotted format.
        /// </summary>
        /// <param name="rdata">The raw data in special format.</param>
        /// <returns>The data in standard dotted format.</returns>
        private string ParseNaptrRDataAndFormat(byte[] rdata) {
            using (var memoryStream = new System.IO.MemoryStream(rdata))
            using (var reader = new System.IO.BinaryReader(memoryStream)) {
                ushort order = (ushort)(reader.ReadByte() << 8 | reader.ReadByte());
                ushort preference = (ushort)(reader.ReadByte() << 8 | reader.ReadByte());

                byte flagsLength = reader.ReadByte();
                string flags = Encoding.ASCII.GetString(reader.ReadBytes(flagsLength));

                byte serviceLength = reader.ReadByte();
                string service = Encoding.ASCII.GetString(reader.ReadBytes(serviceLength));

                byte regexpLength = reader.ReadByte();
                string regexp = Encoding.ASCII.GetString(reader.ReadBytes(regexpLength));

                var replacementBuilder = new StringBuilder();
                byte labelLength = 0; // Initialize to prevent CS0165
                while (memoryStream.Position < memoryStream.Length && (labelLength = reader.ReadByte()) != 0) {
                    if (replacementBuilder.Length > 0) {
                        replacementBuilder.Append('.');
                    }
                    replacementBuilder.Append(Encoding.ASCII.GetString(reader.ReadBytes(labelLength)));
                }
                string replacement = replacementBuilder.ToString();
                if (string.IsNullOrEmpty(replacement) && memoryStream.Position == memoryStream.Length && labelLength == 0) { // Check if it was explicitly a root domain
                    replacement = ".";
                } else if (string.IsNullOrEmpty(replacement) && replacementBuilder.Length == 0 && labelLength !=0 && memoryStream.Position < memoryStream.Length) {
                    // This case can happen if replacement is empty but not the root domain (e.g. NAPTR with empty replacement)
                    // However, RFC3403 implies replacement is a domain-name, which if empty, is the root ".".
                    // For safety, if it's empty and not explicitly root, it might be better to keep it empty or decide a convention.
                    // Current behavior: keeps it empty if not explicitly root.
                }


                return $"{order} {preference} \"{flags}\" \"{service}\" \"{regexp}\" {replacement}";
            }
        }

        private string ConvertSpecialFormatToDotted(string data) {
            if (string.IsNullOrWhiteSpace(data)) return data;

            // Check if the data is already in a standard format. Allow '_' as
            // it commonly appears in service discovery names like "_http".
            if (data.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '_')) {
                return data.TrimEnd('.').ToLowerInvariant();
            }

            var result = new StringBuilder();
            int i = 0;

            while (i < data.Length) {
                // Read the length byte
                int length = data[i];
                if (length == 0) break; // Null terminator indicates the end of the name

                // Move to the next character
                i++;

                // Validate available length before slicing the string
                if (i + length > data.Length) {
                    return data;
                }

                // Read the label
                result.Append(data.Substring(i, length));
                result.Append('.');
                i += length;
            }

            // Remove the trailing dot and return the result
            return result.ToString().TrimEnd('.').ToLowerInvariant();
        }

    }
}
