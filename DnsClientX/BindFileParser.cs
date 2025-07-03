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

                int commentIndex = line.IndexOf(';');
                if (commentIndex >= 0) {
                    line = line.Substring(0, commentIndex).Trim();
                }

                if (line.StartsWith("$TTL", StringComparison.OrdinalIgnoreCase)) {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && int.TryParse(parts[1], out int ttlDirective)) {
                        defaultTtl = ttlDirective;
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
