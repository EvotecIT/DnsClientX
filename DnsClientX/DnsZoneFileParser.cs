using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DnsClientX {
    /// <summary>
    /// Parses common BIND DNS master-file syntax without external dependencies.
    /// </summary>
    public static class DnsZoneFileParser {
        /// <summary>Parses a DNS master file, including permitted relative <c>$INCLUDE</c> files.</summary>
        /// <param name="path">Path to the master file.</param>
        /// <param name="options">Optional parser settings.</param>
        /// <returns>Parsed records and structured diagnostics.</returns>
        public static DnsZoneFileParseResult ParseFile(string path, DnsZoneFileParseOptions? options = null) {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            options ??= new DnsZoneFileParseOptions();
            var records = new List<DnsAnswer>();
            var diagnostics = new List<DnsZoneFileDiagnostic>();
            if (options.DefaultTtl < 0) {
                diagnostics.Add(new DnsZoneFileDiagnostic(path, 1, DnsZoneFileDiagnosticSeverity.Error,
                    "DefaultTtl cannot be negative."));
                return new DnsZoneFileParseResult(records, diagnostics);
            }
            string fullPath = Path.GetFullPath(path);
            string includeRoot = Path.GetFullPath(options.IncludeRootDirectory
                ?? Path.GetDirectoryName(fullPath)
                ?? Directory.GetCurrentDirectory());
            ParseFileCore(fullPath, includeRoot, options, options.Origin, options.DefaultTtl, 0, records, diagnostics);
            return new DnsZoneFileParseResult(records, diagnostics);
        }

        /// <summary>Parses DNS master-file text. Text parsing reports <c>$INCLUDE</c> rather than accessing the filesystem.</summary>
        /// <param name="text">Master-file text.</param>
        /// <param name="options">Optional parser settings.</param>
        /// <param name="sourceName">Logical source name used in diagnostics.</param>
        /// <returns>Parsed records and structured diagnostics.</returns>
        public static DnsZoneFileParseResult Parse(
            string text,
            DnsZoneFileParseOptions? options = null,
            string sourceName = "<text>") {
            if (text == null) throw new ArgumentNullException(nameof(text));
            options ??= new DnsZoneFileParseOptions();
            var records = new List<DnsAnswer>();
            var diagnostics = new List<DnsZoneFileDiagnostic>();
            if (options.DefaultTtl < 0) {
                diagnostics.Add(new DnsZoneFileDiagnostic(sourceName, 1, DnsZoneFileDiagnosticSeverity.Error,
                    "DefaultTtl cannot be negative."));
                return new DnsZoneFileParseResult(records, diagnostics);
            }
            var state = new ParseState(sourceName, null, null, options.Origin, options.DefaultTtl, options, 0, records, diagnostics);
            using var reader = new StringReader(text);
            ParseReader(reader, state);
            return new DnsZoneFileParseResult(records, diagnostics);
        }

        private static void ParseFileCore(
            string path,
            string includeRoot,
            DnsZoneFileParseOptions options,
            string? origin,
            int defaultTtl,
            int depth,
            List<DnsAnswer> records,
            List<DnsZoneFileDiagnostic> diagnostics) {
            if (depth > options.MaxIncludeDepth) {
                diagnostics.Add(new DnsZoneFileDiagnostic(path, 1, DnsZoneFileDiagnosticSeverity.Error, "$INCLUDE nesting exceeds MaxIncludeDepth."));
                return;
            }
            if (!File.Exists(path)) {
                diagnostics.Add(new DnsZoneFileDiagnostic(path, 1, DnsZoneFileDiagnosticSeverity.Error, "Zone file was not found."));
                return;
            }

            try {
                using var reader = File.OpenText(path);
                ParseReader(reader, new ParseState(path, Path.GetDirectoryName(path), includeRoot, origin, defaultTtl, options, depth, records, diagnostics));
            } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                diagnostics.Add(new DnsZoneFileDiagnostic(path, 1, DnsZoneFileDiagnosticSeverity.Error,
                    $"Zone file could not be read: {ex.Message}"));
            }
        }

        private static void ParseReader(TextReader reader, ParseState state) {
            foreach (LogicalLine logical in ReadLogicalLines(reader, state)) {
                List<Token> tokens = Tokenize(logical.Text, logical.Line, state);
                if (tokens.Count == 0) continue;
                if (tokens[0].Value.StartsWith("$", StringComparison.Ordinal)) {
                    ParseDirective(tokens, logical.Line, state);
                } else {
                    ParseRecord(tokens, logical.OwnerOmitted, logical.Line, state);
                }
            }
        }

        private static void ParseDirective(List<Token> tokens, int line, ParseState state) {
            string directive = tokens[0].Value.ToUpperInvariant();
            if (directive == "$ORIGIN") {
                if (tokens.Count < 2) {
                    AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, "$ORIGIN requires a domain name.");
                    return;
                }
                state.Origin = MakeAbsolute(tokens[1].Value, state.Origin).TrimEnd('.');
            } else if (directive == "$TTL") {
                if (tokens.Count < 2 || !TryParseTtl(tokens[1].Value, out int ttl)) {
                    string reason = tokens.Count > 1 && tokens[1].Value.StartsWith("-", StringComparison.Ordinal)
                        ? "negative TTL values are not permitted."
                        : "Invalid $TTL value.";
                    AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, reason);
                    return;
                }
                state.DefaultTtl = ttl;
            } else if (directive == "$INCLUDE") {
                ParseInclude(tokens, line, state);
            } else if (directive == "$GENERATE") {
                ParseGenerate(tokens, line, state);
            } else {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Warning, $"Unsupported directive {tokens[0].Value} was ignored.");
            }
        }

        private static void ParseInclude(List<Token> tokens, int line, ParseState state) {
            if (!state.Options.AllowIncludes) {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, "$INCLUDE is disabled by parser options.");
                return;
            }
            if (tokens.Count < 2 || state.BaseDirectory == null) {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, "$INCLUDE requires file-based parsing and a path.");
                return;
            }

            string includePath = tokens[1].Value;
            if (Path.IsPathRooted(includePath) && !state.Options.AllowUnsafeIncludePaths) {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error,
                    "$INCLUDE rooted paths require AllowUnsafeIncludePaths.");
                return;
            }
            if (!Path.IsPathRooted(includePath)) includePath = Path.Combine(state.BaseDirectory, includePath);
            string fullIncludePath;
            try {
                fullIncludePath = Path.GetFullPath(includePath);
            } catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error,
                    $"Invalid $INCLUDE path: {ex.Message}");
                return;
            }
            if (!state.Options.AllowUnsafeIncludePaths
                && (state.IncludeRootDirectory == null || !IsPathWithinDirectory(fullIncludePath, state.IncludeRootDirectory))) {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error,
                    "$INCLUDE path escapes the permitted include root.");
                return;
            }
            if (!state.Options.AllowUnsafeIncludePaths
                && !TryValidateLinkFreeIncludePath(fullIncludePath, state.IncludeRootDirectory!, out string? linkError)) {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, linkError!);
                return;
            }
            string? includeOrigin = tokens.Count > 2 ? MakeAbsolute(tokens[2].Value, state.Origin).TrimEnd('.') : state.Origin;
            ParseFileCore(fullIncludePath, state.IncludeRootDirectory!, state.Options, includeOrigin, state.DefaultTtl, state.Depth + 1, state.Records, state.Diagnostics);
        }

        private static void ParseGenerate(List<Token> tokens, int line, ParseState state) {
            if (tokens.Count < 5 || !TryParseRange(tokens[1].Value, out int start, out int stop, out int step)) {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, "Invalid $GENERATE directive or range.");
                return;
            }
            long count = (((long)stop - start) / step) + 1;
            if (count > 100000) {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, "$GENERATE expands to more than 100000 records.");
                return;
            }

            for (long index = 0; index < count; index++) {
                int value = checked((int)(start + index * step));
                try {
                    var expanded = tokens.Skip(2)
                        .Select(token => new Token(ExpandGenerate(token.Raw, value), ExpandGenerate(token.Value, value)))
                        .ToList();
                    ParseRecord(expanded, ownerOmitted: false, line, state);
                } catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentOutOfRangeException) {
                    AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error,
                        $"Invalid $GENERATE substitution: {ex.Message}");
                    return;
                }
            }
        }

        private static void ParseRecord(List<Token> tokens, bool ownerOmitted, int line, ParseState state) {
            int index = 0;
            string? owner = null;
            if (!ownerOmitted && !IsRecordModifier(tokens[0].Value)) {
                owner = tokens[index++].Value;
                state.LastOwner = MakeAbsolute(owner, state.Origin);
            } else if (state.LastOwner == null) {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, "An omitted owner name has no preceding owner to inherit.");
                return;
            }

            string absoluteOwner = owner == null ? state.LastOwner! : state.LastOwner!;
            int ttl = state.DefaultTtl;
            DnsRecordType? type = null;
            while (index < tokens.Count) {
                string token = tokens[index].Value;
                if (token.Equals("IN", StringComparison.OrdinalIgnoreCase)) {
                    index++;
                } else if (TryParseTtl(token, out int parsedTtl)) {
                    ttl = parsedTtl;
                    index++;
                } else if (TryParseRecordType(token, out DnsRecordType parsedType)) {
                    type = parsedType;
                    index++;
                    break;
                } else if (token.Equals("CH", StringComparison.OrdinalIgnoreCase) || token.Equals("HS", StringComparison.OrdinalIgnoreCase)) {
                    AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, $"DNS class {token} is not representable by DnsAnswer.");
                    return;
                } else {
                    AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, $"Unexpected record token '{token}'.");
                    return;
                }
            }

            if (!type.HasValue || index >= tokens.Count) {
                AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, "Record type or RDATA is missing.");
                return;
            }

            string data = BuildRdata(tokens.Skip(index).ToList(), type.Value, state.Origin);
            state.Records.Add(new DnsAnswer { Name = absoluteOwner, TTL = ttl, Type = type.Value, DataRaw = data });
        }

        private static string BuildRdata(List<Token> tokens, DnsRecordType type, string? origin) {
            string[] values = tokens.Select(token => token.Value).ToArray();
            if (type == DnsRecordType.TXT || type == DnsRecordType.SPF) {
                return string.Join(" ", tokens.Select(token => token.Raw));
            }

            int[] nameIndexes = type switch {
                DnsRecordType.NS or DnsRecordType.CNAME or DnsRecordType.DNAME or DnsRecordType.PTR => new[] { 0 },
                DnsRecordType.MX or DnsRecordType.AFSDB or DnsRecordType.RT or DnsRecordType.KX => new[] { 1 },
                DnsRecordType.SRV => new[] { 3 },
                DnsRecordType.SOA => new[] { 0, 1 },
                DnsRecordType.NAPTR => new[] { values.Length - 1 },
                DnsRecordType.SVCB or DnsRecordType.HTTPS => new[] { 1 },
                _ => Array.Empty<int>()
            };
            foreach (int nameIndex in nameIndexes.Where(item => item >= 0 && item < values.Length)) {
                values[nameIndex] = MakeAbsolute(values[nameIndex], origin);
            }
            return string.Join(" ", values);
        }

        private static IEnumerable<LogicalLine> ReadLogicalLines(TextReader reader, ParseState state) {
            string? physical;
            int lineNumber = 0;
            int startLine = 0;
            bool ownerOmitted = false;
            int parentheses = 0;
            bool quoted = false;
            bool escaped = false;
            var logical = new StringBuilder();

            while ((physical = reader.ReadLine()) != null) {
                lineNumber++;
                if (logical.Length == 0) {
                    startLine = lineNumber;
                    ownerOmitted = physical.Length > 0 && char.IsWhiteSpace(physical[0]);
                }
                string uncommented = StripComment(physical, ref quoted, ref escaped, ref parentheses);
                if (uncommented.Trim().Length > 0) {
                    if (logical.Length > 0) logical.Append(' ');
                    logical.Append(uncommented.Trim());
                }
                if (parentheses == 0 && !quoted && logical.Length > 0) {
                    yield return new LogicalLine(RemoveGroupingParentheses(logical.ToString()), startLine, ownerOmitted);
                    logical.Clear();
                    escaped = false;
                }
            }

            if (logical.Length > 0) {
                AddDiagnostic(state, startLine, DnsZoneFileDiagnosticSeverity.Error, "Unterminated quoted string or parenthesized record.");
            }
        }

        private static string StripComment(string line, ref bool quoted, ref bool escaped, ref int parentheses) {
            var output = new StringBuilder(line.Length);
            for (int index = 0; index < line.Length; index++) {
                char current = line[index];
                if (escaped) {
                    output.Append(current);
                    escaped = false;
                    continue;
                }
                if (current == '\\') {
                    output.Append(current);
                    escaped = true;
                } else if (current == '"') {
                    output.Append(current);
                    quoted = !quoted;
                } else if (current == ';' && !quoted) {
                    break;
                } else {
                    if (!quoted && current == '(') parentheses++;
                    if (!quoted && current == ')') parentheses--;
                    output.Append(current);
                }
            }
            return output.ToString();
        }

        private static string RemoveGroupingParentheses(string text) {
            var output = new StringBuilder(text.Length);
            bool quoted = false;
            bool escaped = false;
            foreach (char current in text) {
                if (escaped) {
                    output.Append(current);
                    escaped = false;
                } else if (current == '\\') {
                    output.Append(current);
                    escaped = true;
                } else if (current == '"') {
                    output.Append(current);
                    quoted = !quoted;
                } else if (!quoted && (current == '(' || current == ')')) {
                    continue;
                } else {
                    output.Append(current);
                }
            }
            return output.ToString();
        }

        private static List<Token> Tokenize(string text, int line, ParseState state) {
            var tokens = new List<Token>();
            var raw = new StringBuilder();
            var value = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < text.Length; index++) {
                char current = text[index];
                if (char.IsWhiteSpace(current) && !quoted) {
                    AddToken(tokens, raw, value);
                    continue;
                }
                if (current == '"') {
                    quoted = !quoted;
                    raw.Append(current);
                    continue;
                }
                if (current == '\\' && index + 1 < text.Length) {
                    raw.Append(current);
                    value.Append(current);
                    if (index + 3 < text.Length && char.IsDigit(text[index + 1]) && char.IsDigit(text[index + 2]) && char.IsDigit(text[index + 3])) {
                        string digits = text.Substring(index + 1, 3);
                        raw.Append(digits);
                        value.Append(digits);
                        index += 3;
                    } else {
                        raw.Append(text[++index]);
                        value.Append(text[index]);
                    }
                    continue;
                }
                raw.Append(current);
                value.Append(current);
            }
            AddToken(tokens, raw, value);
            if (quoted) AddDiagnostic(state, line, DnsZoneFileDiagnosticSeverity.Error, "Unterminated quoted token.");
            return tokens;
        }

        private static void AddToken(List<Token> tokens, StringBuilder raw, StringBuilder value) {
            if (raw.Length == 0) return;
            tokens.Add(new Token(raw.ToString(), value.ToString()));
            raw.Clear();
            value.Clear();
        }

        private static bool TryParseTtl(string value, out int ttl) {
            ttl = 0;
            if (string.IsNullOrWhiteSpace(value) || value[0] == '-') return false;
            MatchCollection matches = Regex.Matches(value, "([0-9]+)([wdhmsWDHMS]?)", RegexOptions.CultureInvariant);
            if (matches.Count == 0 || string.Concat(matches.Cast<Match>().Select(match => match.Value)) != value) return false;
            long total = 0;
            foreach (Match match in matches) {
                if (!long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long number)) {
                    return false;
                }
                long multiplier = match.Groups[2].Value.ToLowerInvariant() switch {
                    "w" => 604800,
                    "d" => 86400,
                    "h" => 3600,
                    "m" => 60,
                    _ => 1
                };
                if (number > int.MaxValue / multiplier || total > int.MaxValue - number * multiplier) return false;
                total += number * multiplier;
            }
            ttl = (int)total;
            return true;
        }

        private static bool TryParseRecordType(string value, out DnsRecordType type) {
            if (Enum.TryParse(value, true, out type)) return true;
            if (value.StartsWith("TYPE", StringComparison.OrdinalIgnoreCase)
                && ushort.TryParse(value.Substring(4), NumberStyles.None, CultureInfo.InvariantCulture, out ushort numeric)) {
                type = (DnsRecordType)numeric;
                return true;
            }
            return false;
        }

        private static bool IsRecordModifier(string value) {
            return value.Equals("IN", StringComparison.OrdinalIgnoreCase)
                || value.Equals("CH", StringComparison.OrdinalIgnoreCase)
                || value.Equals("HS", StringComparison.OrdinalIgnoreCase)
                || TryParseTtl(value, out _)
                || TryParseRecordType(value, out _);
        }

        private static string MakeAbsolute(string name, string? origin) {
            string value = name.Trim();
            if (value == ".") return value;
            if (value == "@") return string.IsNullOrEmpty(origin) ? "@" : origin!;
            if (value.EndsWith(".", StringComparison.Ordinal) || string.IsNullOrEmpty(origin)) return value;
            return $"{value}.{origin}.";
        }

        private static bool TryParseRange(string value, out int start, out int stop, out int step) {
            start = stop = 0;
            step = 1;
            string[] stepParts = value.Split('/');
            if (stepParts.Length > 2) return false;
            string[] bounds = stepParts[0].Split('-');
            return bounds.Length == 2
                && int.TryParse(bounds[0], NumberStyles.None, CultureInfo.InvariantCulture, out start)
                && int.TryParse(bounds[1], NumberStyles.None, CultureInfo.InvariantCulture, out stop)
                && start <= stop
                && (stepParts.Length == 1 || int.TryParse(stepParts[1], NumberStyles.None, CultureInfo.InvariantCulture, out step))
                && step > 0;
        }

        private static string ExpandGenerate(string template, int value) {
            return Regex.Replace(template, @"\$\{([+-]?\d+),(\d+),([doxX])\}|\$", match => {
                if (match.Value == "$") return value.ToString(CultureInfo.InvariantCulture);
                if (!long.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out long offset)
                    || !int.TryParse(match.Groups[2].Value, NumberStyles.None,
                        CultureInfo.InvariantCulture, out int width)
                    || width > 1024) {
                    throw new FormatException("The offset or width is outside the supported range.");
                }
                long adjusted = checked((long)value + offset);
                string formatted = match.Groups[3].Value switch {
                    "o" => Convert.ToString(adjusted, 8),
                    "x" => adjusted.ToString("x", CultureInfo.InvariantCulture),
                    "X" => adjusted.ToString("X", CultureInfo.InvariantCulture),
                    _ => adjusted.ToString(CultureInfo.InvariantCulture)
                };
                return formatted.PadLeft(width, '0');
            }, RegexOptions.CultureInvariant);
        }

        private static bool IsPathWithinDirectory(string path, string directory) {
            string normalizedDirectory = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(path);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return normalizedPath.StartsWith(normalizedDirectory, comparison);
        }

        private static bool TryValidateLinkFreeIncludePath(string path, string includeRoot, out string? error) {
            string current = NormalizePathWithoutTrailingSeparator(path);
            string root = NormalizePathWithoutTrailingSeparator(includeRoot);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            while (true) {
                try {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) {
                        error = "$INCLUDE paths containing symbolic links or reparse points require AllowUnsafeIncludePaths.";
                        return false;
                    }
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                    error = $"$INCLUDE path safety could not be verified: {ex.Message}";
                    return false;
                }

                if (string.Equals(current, root, comparison)) {
                    error = null;
                    return true;
                }
                DirectoryInfo? parent = Directory.GetParent(current);
                string? normalizedParent = parent == null
                    ? null
                    : NormalizePathWithoutTrailingSeparator(parent.FullName);
                if (normalizedParent == null
                    || (!IsPathWithinDirectory(normalizedParent, root)
                        && !string.Equals(normalizedParent, root, comparison))) {
                    error = "$INCLUDE path escapes the permitted include root.";
                    return false;
                }
                current = normalizedParent;
            }
        }

        private static string NormalizePathWithoutTrailingSeparator(string path) {
            string fullPath = Path.GetFullPath(path);
            string pathRoot = Path.GetPathRoot(fullPath) ?? string.Empty;
            return fullPath.Length > pathRoot.Length
                ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : fullPath;
        }

        private static void AddDiagnostic(ParseState state, int line, DnsZoneFileDiagnosticSeverity severity, string message) {
            state.Diagnostics.Add(new DnsZoneFileDiagnostic(state.Source, line, severity, message));
        }

        private sealed class ParseState {
            internal ParseState(string source, string? baseDirectory, string? includeRootDirectory, string? origin, int defaultTtl, DnsZoneFileParseOptions options, int depth, List<DnsAnswer> records, List<DnsZoneFileDiagnostic> diagnostics) {
                Source = source;
                BaseDirectory = baseDirectory;
                IncludeRootDirectory = includeRootDirectory;
                Origin = string.IsNullOrWhiteSpace(origin) ? null : origin!.Trim().TrimEnd('.');
                DefaultTtl = defaultTtl;
                Options = options;
                Depth = depth;
                Records = records;
                Diagnostics = diagnostics;
            }
            internal string Source { get; }
            internal string? BaseDirectory { get; }
            internal string? IncludeRootDirectory { get; }
            internal string? Origin { get; set; }
            internal int DefaultTtl { get; set; }
            internal string? LastOwner { get; set; }
            internal DnsZoneFileParseOptions Options { get; }
            internal int Depth { get; }
            internal List<DnsAnswer> Records { get; }
            internal List<DnsZoneFileDiagnostic> Diagnostics { get; }
        }

        private readonly record struct Token(string Raw, string Value);
        private readonly record struct LogicalLine(string Text, int Line, bool OwnerOmitted);
    }
}
