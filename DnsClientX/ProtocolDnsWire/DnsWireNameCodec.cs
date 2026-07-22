using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DnsClientX {
    internal static class DnsWireNameCodec {
        internal static string Normalize(string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));
            string value = name.Trim();
            if (value.Length == 0) throw new ArgumentException("DNS name must not be empty.", nameof(name));
            if (value == ".") return ".";

            if (value.IndexOf('\\') >= 0) {
                byte[][] escapedLabels = ParseEscapedLabels(value);
                return string.Join(".", escapedLabels.Select(ToPresentationLabel)) + ".";
            }

            string[] inputLabels = value.TrimEnd('.').Split('.');
            var idn = new IdnMapping();
            var output = new string[inputLabels.Length];
            int wireLength = 1;
            for (int i = 0; i < inputLabels.Length; i++) {
                if (inputLabels[i].Length == 0) throw new ArgumentException("DNS names cannot contain empty labels.", nameof(name));
                string ascii;
                try {
                    ascii = idn.GetAscii(inputLabels[i]);
                } catch (ArgumentException ex) {
                    throw new ArgumentException($"DNS label '{inputLabels[i]}' is not a valid IDN label.", nameof(name), ex);
                }
                int byteLength = Encoding.ASCII.GetByteCount(ascii);
                if (byteLength > 63) throw new ArgumentException("A DNS label cannot exceed 63 octets.", nameof(name));
                wireLength += 1 + byteLength;
                output[i] = ascii;
            }
            if (wireLength > 255) throw new ArgumentException("A DNS name cannot exceed 255 wire-format octets.", nameof(name));
            return string.Join(".", output) + ".";
        }

        internal static byte[][] EncodeLabels(string normalizedName) {
            if (normalizedName == ".") return Array.Empty<byte[]>();
            if (normalizedName.IndexOf('\\') >= 0) return ParseEscapedLabels(normalizedName);
            string[] labels = normalizedName.TrimEnd('.').Split('.');
            var encoded = new byte[labels.Length][];
            for (int i = 0; i < labels.Length; i++) encoded[i] = Encoding.ASCII.GetBytes(labels[i]);
            return encoded;
        }

        internal static string Canonical(string name) => Normalize(name).ToLowerInvariant();

        internal static bool IsSubdomainOrEqual(string name, string parent) {
            string canonicalName = Canonical(name);
            string canonicalParent = Canonical(parent);
            if (canonicalParent == ".") return true;
            return string.Equals(canonicalName, canonicalParent, StringComparison.Ordinal)
                || (canonicalName.Length > canonicalParent.Length
                    && canonicalName.EndsWith(canonicalParent, StringComparison.Ordinal)
                    && canonicalName[canonicalName.Length - canonicalParent.Length - 1] == '.');
        }

        internal static byte[] ToCanonicalWire(string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (name.IndexOf('\\') >= 0) return EscapedPresentationToCanonicalWire(name);
            byte[][] labels = EncodeLabels(Canonical(name));
            var bytes = new List<byte>();
            foreach (byte[] label in labels) {
                bytes.Add((byte)label.Length);
                bytes.AddRange(label);
            }
            bytes.Add(0);
            return bytes.ToArray();
        }

        private static byte[] EscapedPresentationToCanonicalWire(string name) {
            var output = new List<byte>();
            foreach (byte[] parsedLabel in ParseEscapedLabels(name)) {
                var canonicalLabel = parsedLabel
                    .Select(value => value >= 'A' && value <= 'Z' ? (byte)(value + ('a' - 'A')) : value)
                    .ToArray();
                AppendLabel(output, canonicalLabel);
            }
            output.Add(0);
            if (output.Count > 255) throw new ArgumentException("A DNS name cannot exceed 255 wire-format octets.", nameof(name));
            return output.ToArray();
        }

        private static byte[][] ParseEscapedLabels(string name) {
            var labels = new List<byte[]>();
            var label = new List<byte>();
            string value = name.EndsWith(".", StringComparison.Ordinal) ? name.Substring(0, name.Length - 1) : name;
            for (int index = 0; index < value.Length; index++) {
                char current = value[index];
                if (current == '.') {
                    if (label.Count == 0) throw new ArgumentException("DNS names cannot contain empty labels.", nameof(name));
                    labels.Add(label.ToArray());
                    label.Clear();
                    continue;
                }

                int octet;
                if (current == '\\') {
                    if (index + 1 >= value.Length) throw new ArgumentException("DNS presentation name ends with an incomplete escape.", nameof(name));
                    if (index + 3 < value.Length
                        && char.IsDigit(value[index + 1])
                        && char.IsDigit(value[index + 2])
                        && char.IsDigit(value[index + 3])) {
                        octet = (value[index + 1] - '0') * 100 + (value[index + 2] - '0') * 10 + value[index + 3] - '0';
                        if (octet > byte.MaxValue) throw new ArgumentException("DNS presentation name contains an escape above 255.", nameof(name));
                        index += 3;
                    } else {
                        octet = value[++index];
                    }
                } else {
                    if (current > 0x7F) throw new ArgumentException("Escaped DNS presentation names must contain ASCII or decimal octet escapes.", nameof(name));
                    octet = current;
                }

                label.Add((byte)octet);
                if (label.Count > 63) throw new ArgumentException("A DNS label cannot exceed 63 octets.", nameof(name));
            }

            if (label.Count == 0) throw new ArgumentException("DNS names cannot contain empty labels.", nameof(name));
            labels.Add(label.ToArray());
            int wireLength = 1 + labels.Sum(item => item.Length + 1);
            if (wireLength > 255) throw new ArgumentException("A DNS name cannot exceed 255 wire-format octets.", nameof(name));
            return labels.ToArray();
        }

        private static string ToPresentationLabel(byte[] label) {
            var builder = new StringBuilder(label.Length);
            foreach (byte value in label) {
                if (value == (byte)'.' || value == (byte)'\\') {
                    builder.Append('\\').Append((char)value);
                } else if (value < 0x21 || value > 0x7e) {
                    builder.Append('\\').Append(value.ToString("D3", CultureInfo.InvariantCulture));
                } else {
                    builder.Append((char)value);
                }
            }
            return builder.ToString();
        }

        private static void AppendLabel(List<byte> output, IEnumerable<byte> labelValues) {
            byte[] label = labelValues.ToArray();
            if (label.Length > 63) throw new ArgumentException("A DNS label cannot exceed 63 octets.");
            output.Add((byte)label.Length);
            output.AddRange(label);
        }
    }
}
