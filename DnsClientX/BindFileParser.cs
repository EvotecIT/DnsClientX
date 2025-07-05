using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DnsClientX {
    internal static class BindFileParser {
        internal static List<DnsAnswer> ParseZoneFile(string path, Action<string>? debugPrint = null) {
            var records = new List<DnsAnswer>();

            if (!File.Exists(path)) {
                debugPrint?.Invoke($"Skipping {path}; file not found");
                return records;
            }

            int defaultTtl = 3600;

            foreach (var raw in File.ReadLines(path)) {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";")) {
                    continue;
                }

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
                } else {
                    index++;
                }

                if (index >= tokens.Length) {
                    continue;
                }

                string data = string.Join(" ", tokens.Skip(index));

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
