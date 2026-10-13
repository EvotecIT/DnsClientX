using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace DnsClientX;
/// <summary>Parses RFC 6570 URI templates and expands the scalar <c>dns</c> variable for RFC 8484 DoH.</summary>
/// <remarks>All current RFC 6570 operators, variable lists, prefix modifiers, and scalar explode modifiers
/// are supported. Other variables are undefined, including percent-encoded spellings of <c>dns</c>.
/// This type validates template syntax; callers must separately require an HTTPS URI without a fragment.</remarks>
public sealed class DnsDohUriTemplate {
    private readonly IReadOnlyList<Part> _parts;
    /// <summary>Gets the original URI template.</summary>
    public string Template { get; }
    /// <summary>Gets the distinct variable names exactly as written in the template.</summary>
    public IReadOnlyList<string> Variables { get; }

    private DnsDohUriTemplate(string template, List<Part> parts, List<string> variables) {
        Template = template;
        _parts = parts.AsReadOnly();
        Variables = new ReadOnlyCollection<string>(variables.Distinct(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Parses a URI template, rejecting malformed literals, expressions, and modifiers.</summary>
    /// <param name = "template">An absolute or relative RFC 6570 template.</param>
    /// <returns>An immutable parsed template.</returns>
    /// <exception cref = "FormatException">The template does not follow RFC 6570 syntax.</exception>
    public static DnsDohUriTemplate Parse(string template) {
        if (template == null) {
            throw new ArgumentNullException(nameof(template));
        }

        if (template.Length == 0 || template.Length > ushort.MaxValue) {
            throw new FormatException("A DoH URI template must contain 1-65535 characters.");
        }

        var parts = new List<Part>();
        var variables = new List<string>();
        int start = 0;
        for (int position = 0; position < template.Length; position++) {
            if (template[position] != '{') {
                if (template[position] == '}') {
                    throw new FormatException("Unmatched URI-template closing brace.");
                }

                continue;
            }

            if (position > start) {
                parts.Add(new Part(EncodeLiteral(template.Substring(start, position - start))));
            }

            int close = template.IndexOf('}', position + 1);
            if (close < 0) {
                throw new FormatException("Unclosed URI-template expression.");
            }

            string expression = template.Substring(position + 1, close - position - 1);
            if (expression.Length == 0) {
                throw new FormatException("Empty URI-template expression.");
            }

            char operation = '\0';
            if ("+#./;?&".IndexOf(expression[0]) >= 0) {
                operation = expression[0];
                expression = expression.Substring(1);
            }

            var specifications = new List<Variable>();
            foreach (string specification in expression.Split(',')) {
                Variable variable = ParseVariable(specification);
                variables.Add(variable.Name);
                specifications.Add(variable);
            }

            parts.Add(new Part(operation, specifications.ToArray()));
            position = close;
            start = close + 1;
        }

        if (start < template.Length) {
            parts.Add(new Part(EncodeLiteral(template.Substring(start))));
        }

        return new DnsDohUriTemplate(template, parts, variables);
    }

    /// <summary>Expands a DoH GET template using only the unpadded Base64url encoding of the DNS message.</summary>
    /// <param name = "dnsMessage">The complete DNS wire message.</param>
    /// <returns>The expanded URI reference.</returns>
    public string ExpandGet(byte[] dnsMessage) {
        if (dnsMessage == null) {
            throw new ArgumentNullException(nameof(dnsMessage));
        }

        if (!Variables.Contains("dns", StringComparer.Ordinal)) {
            throw new FormatException("DoH GET requires the dns template variable.");
        }

        string encoded = Convert.ToBase64String(dnsMessage).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return Expand(encoded);
    }

    /// <summary>Expands a DoH POST template without defining any variables.</summary>
    /// <returns>The expanded URI reference; the DNS message belongs in the HTTP request body.</returns>
    public string ExpandPost() => Expand(null);
    private string Expand(string? dns) {
        var result = new StringBuilder();
        foreach (Part part in _parts) {
            if (part.Literal != null) {
                result.Append(part.Literal);
                continue;
            }

            bool first = true;
            foreach (Variable variable in part.Variables!) {
                if (dns == null || variable.Name != "dns") {
                    continue;
                }

                string value = variable.Prefix.HasValue && variable.Prefix.Value < dns.Length ? dns.Substring(0, variable.Prefix.Value) : dns;
                if (first) {
                    result.Append(part.Operation switch {
                        '#' => "#",
                        '.' => ".",
                        '/' => "/",
                        ';' => ";",
                        '?' => "?",
                        '&' => "&",
                        _ => ""
                    });
                }
                else {
                    result.Append(part.Operation switch {
                        '.' => ".",
                        '/' => "/",
                        ';' => ";",
                        '?' or '&' => "&",
                        _ => ","
                    });
                }

                if (part.Operation is ';' or '?' or '&') {
                    result.Append(variable.Name);
                    if (value.Length > 0 || part.Operation != ';') {
                        result.Append('=');
                    }
                }

                // Base64url uses only RFC 3986 unreserved characters, so every operator has the same scalar encoding.
                result.Append(value);
                first = false;
            }
        }

        return result.ToString();
    }

    private static Variable ParseVariable(string specification) {
        if (specification.Length == 0) {
            throw new FormatException("Empty URI-template variable.");
        }

        string name = specification;
        int? prefix = null;
        if (name.EndsWith("*", StringComparison.Ordinal)) {
            name = name.Substring(0, name.Length - 1);
        }

        int colon = name.IndexOf(':');
        if (colon >= 0) {
            string length = name.Substring(colon + 1);
            if (specification.EndsWith("*", StringComparison.Ordinal) || length.Length == 0 || length.Length > 4 || length[0] < '1' || length[0] > '9' || length.Any(character => character < '0' || character > '9')) {
                throw new FormatException("Invalid URI-template prefix modifier.");
            }

            prefix = int.Parse(length, System.Globalization.CultureInfo.InvariantCulture);
            name = name.Substring(0, colon);
        }

        bool afterDot = true;
        for (int index = 0; index < name.Length; index++) {
            char character = name[index];
            if (character == '.') {
                if (afterDot) {
                    throw new FormatException("Invalid URI-template variable name.");
                }

                afterDot = true;
                continue;
            }

            if (character == '%') {
                if (index + 2 >= name.Length || !IsHex(name[index + 1]) || !IsHex(name[index + 2])) {
                    throw new FormatException("Invalid variable percent encoding.");
                }

                index += 2;
            }
            else if (!IsAlphaNumeric(character) && character != '_') {
                throw new FormatException("Invalid URI-template variable name or reserved operator.");
            }

            afterDot = false;
        }

        if (afterDot) {
            throw new FormatException("Invalid URI-template variable name.");
        }

        return new Variable(name, prefix);
    }

    private static string EncodeLiteral(string literal) {
        var result = new StringBuilder();
        for (int index = 0; index < literal.Length; index++) {
            char character = literal[index];
            if (character == '%') {
                if (index + 2 >= literal.Length || !IsHex(literal[index + 1]) || !IsHex(literal[index + 2])) {
                    throw new FormatException("Invalid literal percent encoding.");
                }

                result.Append(literal, index, 3);
                index += 2;
            }
            else if (character < 128) {
                if (!IsAlphaNumeric(character) && "-._~:/?#[]@!$&'()*+,;=".IndexOf(character) < 0) {
                    throw new FormatException("Invalid URI-template literal.");
                }

                result.Append(character);
            }
            else {
                int count = 1;
                int scalar = character;
                if (char.IsHighSurrogate(character)) {
                    if (index + 1 >= literal.Length || !char.IsLowSurrogate(literal[index + 1])) {
                        throw new FormatException("Invalid Unicode URI-template literal.");
                    }

                    scalar = char.ConvertToUtf32(character, literal[index + 1]);
                    count = 2;
                }
                else if (char.IsLowSurrogate(character)) {
                    throw new FormatException("Invalid Unicode URI-template literal.");
                }

                // RFC 6570 ucschar/iprivate exclude controls, surrogate code points, and Unicode noncharacters.
                if (scalar < 0xa0 || (scalar >= 0xfdd0 && scalar <= 0xfdef) || (scalar & 0xffff) >= 0xfffe) {
                    throw new FormatException("Invalid Unicode URI-template literal.");
                }

                byte[] bytes = Encoding.UTF8.GetBytes(literal.Substring(index, count));
                foreach (byte value in bytes) {
                    result.Append('%').Append(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                }

                index += count - 1;
            }
        }

        return result.ToString();
    }

    private static bool IsAlphaNumeric(char character) => character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z' || character >= '0' && character <= '9';
    private static bool IsHex(char character) => character >= '0' && character <= '9' || character >= 'a' && character <= 'f' || character >= 'A' && character <= 'F';
    private sealed class Part {
        internal string? Literal { get; }
        internal char Operation { get; }
        internal Variable[]? Variables { get; }

        internal Part(string literal) {
            Literal = literal;
        }

        internal Part(char operation, Variable[] variables) {
            Operation = operation;
            Variables = variables;
        }
    }

    private sealed class Variable {
        internal string Name { get; }
        internal int? Prefix { get; }

        internal Variable(string name, int? prefix) {
            Name = name;
            Prefix = prefix;
        }
    }
}
