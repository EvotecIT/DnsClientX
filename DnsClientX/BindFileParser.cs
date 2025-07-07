using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Helper class capable of parsing simplified BIND style zone files.
    /// </summary>
    internal static class BindFileParser {
        /// <summary>
        /// Parses a BIND zone file and returns DNS answers found in the file.
        /// </summary>
        /// <param name="path">Path to the zone file on disk.</param>
        /// <param name="debugPrint">Optional callback for debug information.</param>
        /// <returns>List of parsed <see cref="DnsAnswer"/> objects.</returns>
        internal static List<DnsAnswer> ParseZoneFile(string path, Action<string>? debugPrint = null) {
            var records = new List<DnsAnswer>();

            if (!File.Exists(path)) {
                debugPrint?.Invoke($"Skipping {path}; file not found");
                return records;
            }

            int defaultTtl = 3600;

            using var enumerator = File.ReadLines(path).GetEnumerator();

            static string StripComments(string line) {
                int commentIndex = -1;
                bool inQuotes = false;
                bool escape = false;
                for (int i = 0; i < line.Length; i++) {
                    char c = line[i];
                    if (escape) {
                        escape = false;
                        continue;
                    }

                    if (c == '\\') {
                        escape = true;
                        continue;
                    }

                    if (c == '"') {
                        inQuotes = !inQuotes;
                        continue;
                    }

                    if (c == ';' && !inQuotes) {
                        commentIndex = i;
                        break;
                    }
                }

                if (commentIndex >= 0) {
                    line = line.Substring(0, commentIndex).Trim();
                }

                return line.Trim();
            }

            static bool QuotesBalanced(string text) {
                bool inQuotes = false;
                bool escape = false;
                for (int i = 0; i < text.Length; i++) {
                    char c = text[i];
                    if (escape) {
                        escape = false;
                        continue;
                    }

                    if (c == '\\') {
                        escape = true;
                        continue;
                    }

                    if (c == '"') {
                        inQuotes = !inQuotes;
                    }
                }

                return !inQuotes;
            }

            while (enumerator.MoveNext()) {
                var line = enumerator.Current.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";")) {
                    continue;
                }

                line = StripComments(line);

                if (line.StartsWith("$TTL", StringComparison.OrdinalIgnoreCase)) {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && int.TryParse(parts[1], out int ttlDirective)) {
                        if (ttlDirective >= 0) {
                            defaultTtl = ttlDirective;
                        } else {
                            debugPrint?.Invoke($"Skipping invalid TTL directive: {line}");
                        }
                    }
                    continue;
                }

                if (line.StartsWith("$ORIGIN", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3) {
                    continue;
                }

                string name = tokens[0];
                int index = 1;
                int ttl = defaultTtl;

                if (int.TryParse(tokens[index], out int ttlVal)) {
                    if (ttlVal < 0) {
                        debugPrint?.Invoke($"Skipping record with negative TTL: {line}");
                        continue;
                    }
                    ttl = ttlVal;
                    index++;
                }

                string typeToken = tokens[index];
                if (!Enum.TryParse(typeToken, true, out DnsRecordType type)) {
                    index++;
                    if (index >= tokens.Length) {
                        continue;
                    }
                    typeToken = tokens[index];
                    if (!Enum.TryParse(typeToken, true, out type)) {
                        continue;
                    }
                    index++;
                } else {
                    index++;
                }

                if (index >= tokens.Length) {
                    continue;
                }

                string data = string.Join(" ", tokens.Skip(index));

                if (type == DnsRecordType.TXT && data.StartsWith("\"")) {
                    if (!QuotesBalanced(data)) {
                        while (enumerator.MoveNext()) {
                            var next = StripComments(enumerator.Current.Trim());
                            data += " " + next;
                            if (QuotesBalanced(data)) {
                                break;
                            }
                        }
                    }

                    if (data.Length > 1 && data.EndsWith("\"")) {
                        data = data.Substring(1, data.Length - 2);
                    }
                }

                records.Add(new DnsAnswer {
                    Name = name,
                    TTL = ttl,
                    Type = type,
                    DataRaw = data
                });
            }

            return records;
        }
    }
}
