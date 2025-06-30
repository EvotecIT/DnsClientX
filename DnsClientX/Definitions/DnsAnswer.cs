using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DnsClientX {
    /// <summary>
    /// DNS answer sent by the server.
    /// </summary>
    public struct DnsAnswer {
        private string _name;
        /// <summary>
        /// This is the name of the record.
        /// Retains original name as returned by the server.
        /// </summary>
        [JsonIgnore]
        public string OriginalName;

        /// <summary>
        /// Temporary storage for filtered data that overrides the normal ConvertData() behavior.
        /// </summary>
        [JsonIgnore]
        private string _filteredData;
        //private string[] _data;

        /// <summary>
        /// This is the name of the record.
        /// Removes the trailing dot if it exists to make it easier to compare with other records across providers.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name {
            get => _name;
            set {
                OriginalName = value;
                _name = value.EndsWith(".") ? value.TrimEnd('.') : value;
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
        public string Data => string.IsNullOrEmpty(_filteredData) ? ConvertData() : _filteredData;

        /// <summary>
        /// The value of the DNS record for the given name and type, split into multiple strings if necessary.
        /// Tries to preserve the original format of the data.
        /// </summary>
        [JsonIgnore]
        public string[] DataStrings => ConvertToMultiString();

        /// <summary>
        /// The value of the DNS record for the given name and type, escaped if necessary removing the quotes completely.
        /// </summary>
        [JsonIgnore]
        public string[] DataStringsEscaped {
            get {
                var data = new List<string>();
                foreach (var item in DataStrings) {
                    data.Add(item.StartsWith("\"") ? item.Replace("\"", "") : item);
                }
                return data.ToArray();
            }
        }

        /// <summary>
        /// Sets filtered data that will override the normal ConvertData() behavior.
        /// This is used internally for filtering operations.
        /// </summary>
        /// <param name="filteredData">The filtered data to return from the Data property.</param>
        internal void SetFilteredData(string filteredData) {
            _filteredData = filteredData;
        }

        /// <summary>
        /// Converts the raw data to multiple strings. By default, DNS records are stored as a single string.
        /// Some records (mainly TXT) can be split into multiple strings and maximum length of a string is 255 characters.
        /// This method tries to preserve the original format of the data in case user needs to check for that format.
        /// </summary>
        /// <returns></returns>
        private string[] ConvertToMultiString() {
            // If we have filtered data, use that instead of the raw data
            string dataToProcess = string.IsNullOrEmpty(_filteredData) ? DataRaw : _filteredData;

            // I'm not sure if this is the best way to do this, but it works for now.
            // This method searches for quotes with space between them or quotes without space
            // Then it splits the string into multiple strings, adding back the quotes that we split on
            var data = new List<string>();
            var temp = new StringBuilder();
            if (dataToProcess != null) {
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
            } else {
                return new string[] { };
            }
        }

        /// <summary>
        /// Converts the data to a string trying to unify the format of the data between different providers
        /// </summary>
        /// <returns></returns>
        private string ConvertData() {
            if (Type == DnsRecordType.TXT) {
                // This is a TXT record. The data is a string enclosed in quotes.
                // The string may be split into multiple strings if it is too long.
                // The strings are enclosed in quotes and separated by a space or without space at all depending on provider

                // First, check if we have properly formatted data with quotes and spaces
                if (DataRaw.Contains("\" \"")) {
                    var result = DataRaw.Replace("\" \"", "").Replace("\"", "");
                    return CleanupTxtRecordData(result);
                }

                // Remove quotes if present for analysis
                string cleanData = DataRaw.Replace("\"", "");

                // Check if the data appears to be concatenated (no line breaks but contains known patterns)
                // Improved detection: also check for obvious concatenation patterns
                bool hasLineBreaks = cleanData.Contains("\n") || cleanData.Contains("\r");
                bool isConcatenated = IsConcatenatedTxtRecord(cleanData);
                bool hasObvious = HasObviousConcatenation(cleanData);

                if (!hasLineBreaks && (isConcatenated || hasObvious)) {
                    return CleanupTxtRecordData(SplitConcatenatedTxtRecord(cleanData));
                }

                // Even if there are line breaks, if the data is obviously concatenated, try to split it
                // This handles cases where Google returns concatenated data with embedded line breaks
                if (hasObvious) {
                    return CleanupTxtRecordData(SplitConcatenatedTxtRecord(cleanData));
                }

                // Default behavior - just remove quotes and clean up empty lines
                return CleanupTxtRecordData(cleanData);
            } else if (Type == DnsRecordType.CAA) {
                // This is a CAA record. Cloudflare returns the data in HEX, so we need to convert it to text.
                // Other providers don't do this.
                if (DataRaw.StartsWith("\\#")) {
                    var parts = DataRaw.Split(' ')
                        .Where(part => !string.IsNullOrEmpty(part))
                        .Select(part => part.Trim())
                        .Where(part => Regex.IsMatch(part, @"\A\b[0-9a-fA-F]+\b\Z"))
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
                } else {
                    return DataRaw;
                }
            } else if (Type == DnsRecordType.DNSKEY) {
                // For DNSKEY records, decode the flags, protocol, algorithm, and public key from the record data
                // Depending on the provider, the data may be in HEX or in text
                // can be: 256 3 ECDSAP256SHA256 oJMRESz5E4gYzS/q6XDrvU1qMPYIjCWzJaOau8XNEZeqCYKD5ar0IRd8KqXXFJkqmVfRvMGPmM1x8fGAa2XhSA==
                // can be: 257 3 13 mdsswUyr3DPW132mOi8V9xESWE8jTo0dxCjjnopKl+GqJxpVXckHAeF+KkxLbxILfDLUT0rAK9iUzy1L53eKGQ==
                var parts = DataRaw.Split(' ');
                if (parts.Length >= 4 && Enum.TryParse<DnsKeyAlgorithm>(parts[2], out var algorithm)) {
                    return $"{parts[0]} {parts[1]} {algorithm} {parts[3]}";
                } else {
                    return DataRaw;
                }
            } else if (Type == DnsRecordType.DS) {
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
                } else {
                    return DataRaw;
                }
            } else if (Type == DnsRecordType.NSEC) {
                // This is a NSEC record. Some providers may return non-standard (google) types.
                // Check if the type is a non-standard type
                var parts = DataRaw.Split(' ');
                foreach (var part in parts) {
                    if (part.StartsWith("TYPE")) {
                        // This is a non-standard type. Try to convert it to a standard type.
                        if (Enum.TryParse<DnsRecordType>(part.Substring(4), out var standardType)) {
                            // The conversion was successful. Replace the non-standard type with the standard type.
                            DataRaw = DataRaw.Replace(part, standardType.ToString());
                        }
                    }
                }

                return DataRaw;
            } else if (Type == DnsRecordType.TLSA) {
                // This is a TLSA record. The data is in HEX.
                // The data is in the format: 3 1 1 2b6e0f
                // The first byte is the certificate usage, the second byte is the selector, the third byte is the matching type, and the rest is the certificate association data
                byte[] parts;
                if (DataRaw.StartsWith("\\#")) {
                    // Handle hexadecimal format
                    parts = DataRaw.Split(' ')
                        .Skip(2) // Skip the first two parts
                        .Where(part => !string.IsNullOrEmpty(part))
                        .Select(part => part.Trim())
                        .Where(part => Regex.IsMatch(part, @"\A\b[0-9a-fA-F]+\b\Z"))
                        .Select(part => Convert.ToByte(part, 16)) // Convert from hexadecimal to byte
                        .ToArray();
                } else if (Regex.IsMatch(DataRaw, @"^\d+ \d+ \d+ [\da-fA-F]+$")) {
                    // If the DataRaw string is already in the correct format, return it as it is
                    return DataRaw;
                } else {
                    // Handle Base64 format
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

            } else if (Type == DnsRecordType.PTR) {
                // For PTR records, decode the domain name from the record data
                try {
                    // First try to decode as Base64
                    var output = Encoding.UTF8.GetString(Convert.FromBase64String(DataRaw));
                    return ConvertSpecialFormatToDotted(output);
                } catch (FormatException) {
                    // If it's not Base64, try to handle it as a special format directly
                    return ConvertSpecialFormatToDotted(DataRaw);
                }
            } else if (Type == DnsRecordType.NAPTR) {
                // NAPTR record (RFC 3403)
                // Handles Base64, Hex, or Plain Text DataRaw
                try {
                    if (DataRaw.StartsWith("\\#")) {
                        // Hex Encoded (e.g., \# XX XX ...)
                        byte[] rdataHex = DataRaw.Split(' ')
                            .Skip(2) // Skip the "\#" and the length byte
                            .Where(part => !string.IsNullOrEmpty(part))
                            .Select(part => part.Trim())
                            .Where(part => Regex.IsMatch(part, @"\A\b[0-9a-fA-F]{1,2}\b\Z")) // Match 1 or 2 hex chars
                            .Select(part => Convert.ToByte(part, 16))
                            .ToArray();
                        if (rdataHex.Length > 4) { // Basic validation for minimum RDATA length
                             return ParseNaptrRDataAndFormat(rdataHex);
                        }
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"Error parsing NAPTR record from Hex: {ex.Message} for DataRaw: {DataRaw}");
                    // Fall through to try other formats or return DataRaw at the end
                }

                try {
                    // Attempt Base64 Decoding
                    byte[] rdataBase64 = Convert.FromBase64String(DataRaw);
                    return ParseNaptrRDataAndFormat(rdataBase64);
                } catch (FormatException) {
                    // Not Base64, try parsing as plain text
                } catch (Exception ex) {
                    Console.WriteLine($"Error parsing NAPTR record from Base64: {ex.Message} for DataRaw: {DataRaw}");
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
                            regexp = "";
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
                    Console.WriteLine($"Error parsing NAPTR record from plain text: {ex.Message} for DataRaw: {DataRaw}");
                }

                // If all parsing attempts fail or if it's an unrecognized format for NAPTR that didn't cleanly parse
                Console.WriteLine($"NAPTR DataRaw '{DataRaw}' did not match known Hex, Base64, or plain text patterns, or failed parsing.");
                return DataRaw; // Fallback
            } else {
                // Some records return the data in a higher case (microsoft.com/NS/Quad9ECS) which needs to be fixed
                return DataRaw.ToLower();
            }
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

            // Check if the data is already in a standard format
            if (data.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '.')) {
                return data.TrimEnd('.').ToLower();
            }

            var result = new StringBuilder();
            int i = 0;

            while (i < data.Length) {
                // Read the length byte
                int length = data[i];
                if (length == 0) break; // Null terminator indicates the end of the name

                // Move to the next character
                i++;

                // Read the label
                if (i + length <= data.Length) {
                    result.Append(data.Substring(i, length));
                    result.Append('.');
                    i += length;
                } else {
                    // If the length byte is invalid, break the loop
                    break;
                }
            }

            // Remove the trailing dot and return the result
            return result.ToString().TrimEnd('.').ToLower();
        }

        /// <summary>
        /// Determines if a TXT record appears to be concatenated based on common patterns
        /// </summary>
        /// <param name="data">The cleaned TXT record data</param>
        /// <returns>True if the record appears to be concatenated</returns>
        private bool IsConcatenatedTxtRecord(string data) {
            if (string.IsNullOrWhiteSpace(data)) return false;

            // Common patterns that indicate concatenated records
            var patterns = new[] {
                "=", // Most TXT records are key=value pairs
                "v=spf1", // SPF records
                "google-site-verification=",
                "facebook-domain-verification=",
                "apple-domain-verification=",
                "MS=ms",
                "_domainkey",
                "dmarc",
                "adsp"
            };

            int patternMatches = 0;
            foreach (var pattern in patterns) {
                var matches = CountOccurrences(data, pattern);
                if (matches > 1) {
                    patternMatches += matches - 1; // Subtract 1 because first occurrence is expected
                }
            }

            // More lenient threshold - if we have any duplicate patterns, it's likely concatenated
            return patternMatches >= 1;
        }

        /// <summary>
        /// Splits concatenated TXT records into separate lines
        /// </summary>
        /// <param name="data">The concatenated TXT record data</param>
        /// <returns>Properly separated TXT records</returns>
        private string SplitConcatenatedTxtRecord(string data) {
            if (string.IsNullOrWhiteSpace(data)) return data;

            var result = new List<string>();
            var currentRecord = new StringBuilder();

            // Common prefixes that typically start new TXT records
            var recordPrefixes = new[] {
                "v=spf1",
                "google-site-verification=",
                "facebook-domain-verification=",
                "apple-domain-verification=",
                "MS=ms",
                "_domainkey=",
                "dmarc=",
                "adsp="
            };

            // Split the data into potential records based on known patterns
            var segments = new List<string>();
            int lastSplit = 0;

            for (int i = 0; i < data.Length; i++) {
                // Check if we're at the start of a new record
                foreach (var prefix in recordPrefixes) {
                    if (i > 0 && i + prefix.Length <= data.Length &&
                        data.Substring(i, prefix.Length).Equals(prefix, StringComparison.OrdinalIgnoreCase)) {
                        // Found a new record start - split here
                        if (i > lastSplit) {
                            var segment = data.Substring(lastSplit, i - lastSplit);
                            if (!string.IsNullOrWhiteSpace(segment)) {
                                segments.Add(segment);
                            }
                        }
                        lastSplit = i;
                        break;
                    }
                }
            }

            // Add the last segment
            if (lastSplit < data.Length) {
                var segment = data.Substring(lastSplit);
                if (!string.IsNullOrWhiteSpace(segment)) {
                    segments.Add(segment);
                }
            }

            // If we didn't find clear prefixes, try a more sophisticated approach
            if (segments.Count <= 1) {
                segments = SplitByEqualsPattern(data);
            }

            // Join segments with newlines
            return string.Join("\n", segments.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        /// <summary>
        /// Splits concatenated records by analyzing equals sign patterns
        /// </summary>
        /// <param name="data">The concatenated data</param>
        /// <returns>List of potential record segments</returns>
        private List<string> SplitByEqualsPattern(string data) {
            var segments = new List<string>();
            var currentSegment = new StringBuilder();
            bool inValue = false;
            int equalsCount = 0;

            for (int i = 0; i < data.Length; i++) {
                char c = data[i];
                currentSegment.Append(c);

                if (c == '=') {
                    if (!inValue) {
                        inValue = true;
                        equalsCount++;
                    }
                } else if (inValue && char.IsLetter(c) && i > 0) {
                    // Check if this might be the start of a new key (letter after completing a value)
                    // Look ahead to see if there's an equals sign coming up
                    bool isNewKey = false;
                    for (int j = i; j < Math.Min(i + 50, data.Length); j++) {
                        if (data[j] == '=') {
                            isNewKey = true;
                            break;
                        }
                        if (!char.IsLetterOrDigit(data[j]) && data[j] != '-' && data[j] != '_' && data[j] != '.') {
                            break;
                        }
                    }

                    if (isNewKey && currentSegment.Length > 1) {
                        // Remove the current character from current segment and start a new one
                        currentSegment.Length--;
                        var segment = currentSegment.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(segment)) {
                            segments.Add(segment);
                        }
                        currentSegment.Clear();
                        currentSegment.Append(c);
                        inValue = false;
                    }
                }
            }

            // Add the last segment
            var lastSegment = currentSegment.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(lastSegment)) {
                segments.Add(lastSegment);
            }

            return segments;
        }

        /// <summary>
        /// Counts occurrences of a substring in a string
        /// </summary>
        /// <param name="text">The text to search in</param>
        /// <param name="pattern">The pattern to search for</param>
        /// <returns>Number of occurrences</returns>
        private int CountOccurrences(string text, string pattern) {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern)) return 0;

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) >= 0) {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        /// <summary>
        /// More aggressive detection for obvious concatenation patterns
        /// </summary>
        /// <param name="data">The cleaned TXT record data</param>
        /// <returns>True if obvious concatenation is detected</returns>
        private bool HasObviousConcatenation(string data) {
            if (string.IsNullOrWhiteSpace(data)) return false;

            // Look for specific concatenation patterns that are obvious
            var obviousPatterns = new[] {
                "google-site-verification=",
                "facebook-domain-verification=",
                "apple-domain-verification=",
                "v=spf1"
            };

            // If any pattern appears multiple times, it's definitely concatenated
            foreach (var pattern in obviousPatterns) {
                if (CountOccurrences(data, pattern) > 1) {
                    return true;
                }
            }

            // Check for pattern combinations that indicate concatenation
            // e.g., if we have both SPF and Google verification, they should be separate records
            bool hasSpf = data.Contains("v=spf1");
            bool hasGoogleVerification = data.Contains("google-site-verification=");
            bool hasFacebookVerification = data.Contains("facebook-domain-verification=");
            bool hasAppleVerification = data.Contains("apple-domain-verification=");
            bool hasMsVerification = data.Contains("MS=ms");

            // Count how many different verification types we have
            int verificationTypes = 0;
            if (hasSpf) verificationTypes++;
            if (hasGoogleVerification) verificationTypes++;
            if (hasFacebookVerification) verificationTypes++;
            if (hasAppleVerification) verificationTypes++;
            if (hasMsVerification) verificationTypes++;

            // If we have multiple verification types in one "record", it's concatenated
            if (verificationTypes >= 2) {
                return true;
            }

            // Check for consecutive equals signs which often indicate concatenation
            // This catches cases like "...valuekey=anothervalue"
            int consecutiveEquals = 0;
            for (int i = 0; i < data.Length - 1; i++) {
                if (data[i] == '=' && char.IsLetter(data[i + 1])) {
                    // Look ahead to see if there's another equals sign soon
                    for (int j = i + 1; j < Math.Min(i + 50, data.Length); j++) {
                        if (data[j] == '=') {
                            consecutiveEquals++;
                            break;
                        }
                        if (!char.IsLetterOrDigit(data[j]) && data[j] != '-' && data[j] != '_' && data[j] != '.') {
                            break;
                        }
                    }
                }
            }

            return consecutiveEquals >= 2;
        }
    }
}
