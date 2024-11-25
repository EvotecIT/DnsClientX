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
        public string Data => ConvertData();

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
        /// Converts the raw data to multiple strings. By default, DNS records are stored as a single string.
        /// Some records (mainly TXT) can be split into multiple strings and maximum length of a string is 255 characters.
        /// This method tries to preserve the original format of the data in case user needs to check for that format.
        /// </summary>
        /// <returns></returns>
        private string[] ConvertToMultiString() {
            // I'm not sure if this is the best way to do this, but it works for now.
            // This method searches for quotes with space between them or quotes without space
            // Then it splits the string into multiple strings, adding back the quotes that we split on
            var data = new List<string>();
            var temp = new StringBuilder();
            if (DataRaw != null) {
                for (int i = 0; i < DataRaw.Length; i++) {
                    if (i < DataRaw.Length - 1 && DataRaw[i] == '"' && DataRaw[i + 1] == '"') {
                        temp.Append(DataRaw[i]);
                        data.Add(temp.ToString());
                        temp.Clear();
                        temp.Append("\""); // Add quotes back
                        i++; // Skip the next character as it's part of the split
                    } else if (i < DataRaw.Length - 2 && DataRaw[i] == '"' && DataRaw[i + 1] == ' ' && DataRaw[i + 2] == '"') {
                        temp.Append(DataRaw[i]);
                        data.Add(temp.ToString());
                        temp.Clear();
                        temp.Append("\""); // Add quotes back
                        i += 2; // Skip the next two characters as they're part of the split
                    } else {
                        temp.Append(DataRaw[i]);
                    }
                }

                if (temp.Length > 0) {
                    data.Add(temp.ToString());
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
                return DataRaw.Replace("\" \"", "").Replace("\"", "");
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
            } else {
                // Some records return the data in a higher case (microsoft.com/NS/Quad9ECS) which needs to be fixed
                return DataRaw.ToLower();
            }
        }

        /// <summary>
        /// Converts a special format like "\u0003one\u0003one\u0003one\u0003one\0" to a standard dotted format.
        /// </summary>
        /// <param name="data">The raw data in special format.</param>
        /// <returns>The data in standard dotted format.</returns>
        private string ConvertSpecialFormatToDotted(string data) {
            if (string.IsNullOrWhiteSpace(data)) return data;

            // Replace all variants of length-3 markers with dots
            var result = data.Replace("\\u0003", ".")
                            .Replace("\\003", ".")
                            .Replace("\u0003", ".")
                            .Replace("\0", "");  // Remove null terminators

            // Clean up: remove leading/trailing dots and normalize multiple dots
            return Regex.Replace(result, "\\.{2,}", ".")  // Replace multiple dots with single dot
                       .Trim('.')                         // Remove leading/trailing dots
                       .ToLower();                        // Normalize case
        }
    }
}
