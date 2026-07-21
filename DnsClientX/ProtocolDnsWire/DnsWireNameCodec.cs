using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DnsClientX {
    internal static class DnsWireNameCodec {
        internal static string Normalize(string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));
            string value = name.Trim();
            if (value.Length == 0) throw new ArgumentException("DNS name must not be empty.", nameof(name));
            if (value == ".") return ".";

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
            string[] labels = normalizedName.TrimEnd('.').Split('.');
            var encoded = new byte[labels.Length][];
            for (int i = 0; i < labels.Length; i++) encoded[i] = Encoding.ASCII.GetBytes(labels[i]);
            return encoded;
        }

        internal static string Canonical(string name) => Normalize(name).ToLowerInvariant();

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
            var label = new List<byte>();
            string value = name.EndsWith(".", StringComparison.Ordinal) ? name.Substring(0, name.Length - 1) : name;
            for (int i = 0; i < value.Length; i++) {
                char current = value[i];
                if (current == '.') {
                    AppendLabel(output, label);
                    label.Clear();
                    continue;
                }
                int octet;
                if (current == '\\') {
                    if (i + 1 >= value.Length) throw new ArgumentException("DNS presentation name ends with an incomplete escape.", nameof(name));
                    if (i + 3 < value.Length && char.IsDigit(value[i + 1]) && char.IsDigit(value[i + 2]) && char.IsDigit(value[i + 3])) {
                        octet = (value[i + 1] - '0') * 100 + (value[i + 2] - '0') * 10 + value[i + 3] - '0';
                        if (octet > byte.MaxValue) throw new ArgumentException("DNS presentation name contains an escape above 255.", nameof(name));
                        i += 3;
                    } else {
                        octet = value[++i];
                    }
                } else {
                    if (current > 0x7F) throw new ArgumentException("Escaped DNS presentation names must contain ASCII or decimal octet escapes.", nameof(name));
                    octet = current;
                }
                if (octet >= 'A' && octet <= 'Z') octet += 'a' - 'A';
                label.Add((byte)octet);
            }
            AppendLabel(output, label);
            output.Add(0);
            if (output.Count > 255) throw new ArgumentException("A DNS name cannot exceed 255 wire-format octets.", nameof(name));
            return output.ToArray();
        }

        private static void AppendLabel(List<byte> output, List<byte> label) {
            if (label.Count > 63) throw new ArgumentException("A DNS label cannot exceed 63 octets.");
            output.Add((byte)label.Count);
            output.AddRange(label);
        }
    }
}
