using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DnsClientX;

internal static partial class DnsSvcbCodec {
    internal static bool TryParse(string text, out SvcbRecord? record) {
        record = null;
        var tokens = DnsPresentationFormat.Tokenize(text, out bool complete);
        if (!complete || tokens.Count < 2 || !ushort.TryParse(tokens[0].Raw,
            NumberStyles.None, CultureInfo.InvariantCulture, out ushort priority)) return false;
        try {
            var parameters = new List<SvcbParameter>();
            foreach (var token in tokens.Skip(2)) {
                int separator = token.Raw.IndexOf('=');
                string name = separator < 0 ? token.Raw : token.Raw.Substring(0, separator);
                if (!TryKey(name, out ushort key, out bool numeric)) return false;
                string presentationValue = separator < 0 ? string.Empty : token.Raw.Substring(separator + 1);
                // These named parameter grammars explicitly prohibit DNS escape sequences.
                if (!numeric && (key is 0 or 3 or 4 or 5 or 6 or 9) && presentationValue.IndexOf('\\') >= 0) return false;
                byte[] value = DecodeString(presentationValue);
                parameters.Add(new SvcbParameter(key, numeric ? value : EncodeParameter(key, value)));
            }
            record = new SvcbRecord(priority, tokens[1].Raw, parameters);
            return true;
        } catch (ArgumentException) { return false; }
        catch (FormatException) { return false; }
    }

    internal static string Format(SvcbRecord record, bool normalizeTarget = true) {
        string target = normalizeTarget ? record.Target : record.OriginalTarget;
        string prefix = record.Priority.ToString(CultureInfo.InvariantCulture) + " " + target;
        return record.Parameters.Count == 0 ? prefix : prefix + " "
            + string.Join(" ", record.Parameters.Values.Select(FormatParameter));
    }

    private static byte[] EncodeParameter(ushort key, byte[] value) {
        string text = new string(value.Select(octet => (char)octet).ToArray());
        switch (key) {
            case 0:
                var keys = new List<ushort>();
                foreach (string name in text.Split(',')) {
                    if (!TryKey(name, out ushort required, out _)) throw new FormatException("Unknown mandatory key name.");
                    keys.Add(required);
                }
                return keys.OrderBy(required => required).SelectMany(required => new[] { (byte)(required >> 8), (byte)required }).ToArray();
            case 1:
            case 10:
                return DecodeList(value, allowEmpty: key == 10).SelectMany(identifier => {
                    if (identifier.Length > byte.MaxValue) throw new FormatException("ALPN identifier is too long.");
                    return new[] { (byte)identifier.Length }.Concat(identifier);
                }).ToArray();
            case 3:
                if (!ushort.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ushort port)) throw new FormatException("Invalid SVCB port.");
                return new[] { (byte)(port >> 8), (byte)port };
            case 4:
            case 6:
                return text.Split(',').SelectMany(item => {
                    if (!IPAddress.TryParse(item, out var address) || address.AddressFamily != (key == 4
                        ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6)
                        || key == 4 && !IsCanonicalIpv4(item)
                        || key == 6 && (item.IndexOf('%') >= 0 || item.IndexOf('.') >= 0
                            && !IsCanonicalIpv4(item.Substring(item.LastIndexOf(':') + 1)))) throw new FormatException("Invalid SVCB address hint.");
                    return address.GetAddressBytes();
                }).ToArray();
            case 5:
                return Convert.FromBase64String(text);
            case 9:
                return EncodeGroups(text);
            case 12:
                return EncodeTransportWeights(text);
            default:
                return value;
        }
    }

    // IPAddress accepts legacy shorthand, hexadecimal and octal IPv4 forms. SVCB
    // requires dotted decimal, including when IPv4 is embedded in an IPv6 hint.
    private static bool IsCanonicalIpv4(string text) => IPAddress.TryParse(text, out var address)
        && address.AddressFamily == AddressFamily.InterNetwork
        && string.Equals(text, address.ToString(), StringComparison.Ordinal);

    private static string FormatParameter(SvcbParameter parameter) {
        byte[] value = parameter.Bytes;
        string name = parameter.Name;
        switch (parameter.Key) {
            case 0:
                return name + "=" + string.Join(",", ReadKeys(value).Select(KeyName));
            case 1:
            case 10:
                // Comma-list escaping is applied before DNS character-string escaping.
                if (parameter.Key == 10 && value.Length == 1 && value[0] == 0) return "key10=" + Quote(value);
                var list = new List<byte>();
                bool first = true;
                foreach (string identifier in ReadAlpn(value, allowEmpty: parameter.Key == 10)) {
                    if (!first) list.Add((byte)',');
                    first = false;
                    foreach (char octet in identifier) {
                        if (octet is ',' or '\\') list.Add((byte)'\\');
                        list.Add((byte)octet);
                    }
                }
                return name + "=" + Quote(list.ToArray());
            case 2:
            case 8:
            case 11:
                return name;
            case 3:
                return name + "=" + ((value[0] << 8) | value[1]).ToString(CultureInfo.InvariantCulture);
            case 4:
            case 6:
                int width = parameter.Key == 4 ? 4 : 16;
                var addresses = new List<string>();
                for (int offset = 0; offset < value.Length; offset += width) {
                    var address = new byte[width];
                    Buffer.BlockCopy(value, offset, address, 0, width);
                    addresses.Add(new IPAddress(address).ToString());
                }
                return name + "=" + string.Join(",", addresses);
            case 5:
                return name + "=" + Convert.ToBase64String(value);
            case 9:
                return name + "=" + string.Join(",", ReadKeys(value).Select(group => group.ToString(CultureInfo.InvariantCulture)));
            case 12:
                // Preserve received weight octets, including values above 100. The draft
                // clamps their interpretation, so generic notation retains exact evidence.
                return ReadTransportWeights(value).Any(entry => entry.Value > 100 || entry.Key.Any(character => character is ',' or ':' || char.IsWhiteSpace(character)))
                    ? "key12=" + Quote(value)
                    : name + "=" + Quote(Encoding.ASCII.GetBytes(string.Join(",", ReadTransportWeights(value)
                        .Select(entry => entry.Key + ":" + entry.Value.ToString(CultureInfo.InvariantCulture)))));
            default:
                return name + "=" + Quote(value);
        }
    }

    private static byte[] DecodeString(string text) {
        bool quoted = text.StartsWith("\"", StringComparison.Ordinal);
        int end = quoted ? text.Length - 1 : text.Length;
        if (quoted && (text.Length < 2 || text[end] != '"')) throw new FormatException("Incomplete SVCB quoted value.");
        var bytes = new List<byte>();
        for (int index = quoted ? 1 : 0; index < end; index++) {
            int octet = text[index];
            if (octet == '\\') {
                if (++index >= end) throw new FormatException("Incomplete SVCB escape.");
                octet = text[index];
                if (octet >= '0' && octet <= '9') {
                    if (index + 2 >= end || !IsDigit(text[index + 1]) || !IsDigit(text[index + 2])) throw new FormatException("SVCB decimal escape requires three digits.");
                    octet = (octet - '0') * 100 + (text[index + 1] - '0') * 10 + text[index + 2] - '0';
                    index += 2;
                }
            } else if (octet == '"' || !quoted && char.IsWhiteSpace((char)octet)) {
                throw new FormatException("Invalid SVCB character-string.");
            }
            if (octet > byte.MaxValue) throw new FormatException("SVCB character-string must represent octets.");
            bytes.Add((byte)octet);
        }
        return bytes.ToArray();
    }

    private static IEnumerable<byte[]> DecodeList(byte[] bytes, bool allowEmpty = false) {
        if (allowEmpty && bytes.Length == 0) yield break;
        var item = new List<byte>();
        for (int index = 0; index <= bytes.Length; index++) {
            if (index == bytes.Length || bytes[index] == (byte)',') {
                if (!allowEmpty && item.Count == 0) throw new FormatException("SVCB list items must be nonempty.");
                yield return item.ToArray();
                item.Clear();
            } else if (bytes[index] == (byte)'\\') {
                if (++index == bytes.Length || bytes[index] is not ((byte)',') and not ((byte)'\\')) throw new FormatException("Invalid SVCB list escape.");
                item.Add(bytes[index]);
            } else {
                item.Add(bytes[index]);
            }
        }
    }

    private static bool IsDigit(char value) => value >= '0' && value <= '9';

    private static string Quote(byte[] bytes) {
        var text = new StringBuilder().Append('"');
        foreach (byte octet in bytes) {
            if (octet is (byte)'"' or (byte)'\\') text.Append('\\').Append((char)octet);
            else if (octet < 0x20 || octet > 0x7E) text.Append('\\').Append(octet.ToString("D3", CultureInfo.InvariantCulture));
            else text.Append((char)octet);
        }
        return text.Append('"').ToString();
    }
}
