using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DnsClientX {
    internal static class BindFileParser {
        internal static List<DnsAnswer> ParseZoneFile(string path, Action<string>? debugPrint = null) {
            DnsZoneFileParseResult result = DnsZoneFileParser.ParseFile(path);
            foreach (DnsZoneFileDiagnostic diagnostic in result.Diagnostics) {
                debugPrint?.Invoke(diagnostic.ToString());
            }
            return result.Records.Select(record => {
                if (record.Type != DnsRecordType.TXT && record.Type != DnsRecordType.SPF) {
                    return record;
                }

                DnsAnswer compatible = record;
                MatchCollection segments = Regex.Matches(record.DataRaw, "\\\"((?:\\\\.|[^\\\"])*)\\\"");
                compatible.DataRaw = segments.Count == 0
                    ? record.DataRaw
                    : string.Join(" ", segments.Cast<Match>().Select(match => match.Groups[1].Value));
                compatible.DataRaw = compatible.DataRaw.Replace("\\n", "\n");
                return compatible;
            }).ToList();
        }
    }
}
