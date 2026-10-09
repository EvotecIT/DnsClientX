using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DnsClientX;

internal static partial class DnsSvcbCodec {
    // IANA keys 9-12 use their registered wire grammars. They remain parameters,
    // without adding TLS, CoAP, provisioning-domain or transport-selection clients.
    private static byte[] EncodeGroups(string text) => text.Split(',').SelectMany(item => {
        if (!ushort.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out ushort group))
            throw new FormatException("Invalid TLS supported group.");
        return new[] { (byte)(group >> 8), (byte)group };
    }).ToArray();

    private static byte[] EncodeTransportWeights(string text) {
        var bytes = new List<byte>();
        foreach (string item in text.Split(',')) {
            int separator = item.IndexOf(':');
            if (separator <= 0 || !byte.TryParse(item.Substring(separator + 1), NumberStyles.None,
                CultureInfo.InvariantCulture, out byte weight) || weight > 100)
                throw new FormatException("Invalid oots transport weight.");
            string protocol = item.Substring(0, separator);
            if (protocol.Length > byte.MaxValue || protocol.Any(value => value > 0x7f))
                throw new FormatException("Invalid oots protocol identifier.");
            bytes.Add((byte)protocol.Length);
            bytes.AddRange(Encoding.ASCII.GetBytes(protocol));
            bytes.Add(weight);
        }
        return bytes.ToArray();
    }

    private static IReadOnlyList<KeyValuePair<string, byte>> ReadTransportWeights(byte[] bytes) {
        var entries = new List<KeyValuePair<string, byte>>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int offset = 0; offset < bytes.Length;) {
            int length = bytes[offset++];
            if (length == 0 || length >= bytes.Length - offset)
                throw new ArgumentException("Truncated oots protocol entry.");
            for (int index = 0; index < length; index++) {
                if (bytes[offset + index] > 0x7f) throw new ArgumentException("Non-ASCII oots protocol identifier.");
            }
            string protocol = Encoding.ASCII.GetString(bytes, offset, length);
            offset += length;
            if (!names.Add(protocol)) throw new ArgumentException("Duplicate oots protocol identifier.");
            entries.Add(new KeyValuePair<string, byte>(protocol, bytes[offset++]));
        }
        if (entries.Count == 0) throw new ArgumentException("An oots parameter requires an entry.");
        return entries;
    }
}
