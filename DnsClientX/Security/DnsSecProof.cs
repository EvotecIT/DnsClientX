using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace DnsClientX {
    internal static class DnsSecProof {
        private const ushort MaxSupportedNsec3Iterations = 500;

        internal static bool ProvesUnsignedDelegation(DnsResponse response, string name) {
            string canonicalName = DnsWireNameCodec.Canonical(name);
            var nsec3 = new List<(string OwnerHash, string Zone, Nsec3Value Value)>();
            foreach (DnsWireResourceRecord record in DnsSecWire.Records(response)) {
                if (record.Type == DnsRecordType.NSEC &&
                    string.Equals(DnsWireNameCodec.Canonical(record.Name), canonicalName, StringComparison.Ordinal) &&
                    TryReadNsec(response.WireMessage, record, out _, out HashSet<ushort> types)) {
                    return types.Contains((ushort)DnsRecordType.NS) &&
                           !types.Contains((ushort)DnsRecordType.SOA) &&
                           !types.Contains((ushort)DnsRecordType.DS);
                }
                if (record.Type == DnsRecordType.NSEC3 && TryReadNsec3(response.WireMessage, record, out Nsec3Value value)) {
                    string owner = DnsWireNameCodec.Canonical(record.Name);
                    int dot = owner.IndexOf('.');
                    if (dot > 0) nsec3.Add((owner.Substring(0, dot), owner.Substring(dot + 1), value));
                }
            }
            if (nsec3.Count == 0) return false;
            Nsec3Value parameters = nsec3[0].Value;
            if (nsec3.Any(item => item.Value.HashAlgorithm != parameters.HashAlgorithm ||
                                  item.Value.Iterations != parameters.Iterations ||
                                  !item.Value.Salt.SequenceEqual(parameters.Salt))) return false;

            string candidate = ToBase32Hex(HashName(canonicalName, parameters.Iterations, parameters.Salt));
            (string OwnerHash, string Zone, Nsec3Value Value) exact = nsec3.FirstOrDefault(item =>
                string.Equals(item.OwnerHash, candidate, StringComparison.OrdinalIgnoreCase));
            if (exact.OwnerHash != null) {
                return exact.Value.Types.Contains((ushort)DnsRecordType.NS) &&
                       !exact.Value.Types.Contains((ushort)DnsRecordType.SOA) &&
                       !exact.Value.Types.Contains((ushort)DnsRecordType.DS);
            }

            string? closest = Ancestors(canonicalName).Skip(1).FirstOrDefault(ancestor => {
                string hash = ToBase32Hex(HashName(ancestor, parameters.Iterations, parameters.Salt));
                return nsec3.Any(item => string.Equals(item.OwnerHash, hash, StringComparison.OrdinalIgnoreCase));
            });
            if (closest == null || !string.Equals(NextCloserName(canonicalName, closest), canonicalName, StringComparison.Ordinal)) return false;
            return nsec3.Any(item => item.Value.OptOut &&
                CoversHash(item.OwnerHash, ToBase32Hex(item.Value.NextHash), candidate));
        }

        internal static bool ProvesNoData(DnsResponse response, string name, DnsRecordType type) {
            string canonicalName = DnsWireNameCodec.Canonical(name);
            foreach (DnsWireResourceRecord record in DnsSecWire.Records(response)) {
                if (record.Type == DnsRecordType.NSEC &&
                    string.Equals(DnsWireNameCodec.Canonical(record.Name), canonicalName, StringComparison.Ordinal) &&
                    TryReadNsec(response.WireMessage, record, out _, out HashSet<ushort> types) &&
                    !types.Contains((ushort)type) && !types.Contains((ushort)DnsRecordType.CNAME)) return true;
            }

            return ProvesNsec3NoData(response, canonicalName, type);
        }

        internal static bool ProvesNameError(DnsResponse response, string name) {
            string canonicalName = DnsWireNameCodec.Canonical(name);
            var nsecs = new List<(string Owner, string Next, HashSet<ushort> Types)>();
            foreach (DnsWireResourceRecord record in DnsSecWire.Records(response)) {
                if (record.Type == DnsRecordType.NSEC &&
                    TryReadNsec(response.WireMessage, record, out string next, out HashSet<ushort> types)) {
                    nsecs.Add((DnsWireNameCodec.Canonical(record.Name), next, types));
                }
            }
            if (nsecs.Count == 0) return ProvesNsec3NameError(response, canonicalName);

            string? closestEncloser = null;
            foreach (string ancestor in Ancestors(canonicalName)) {
                (string Owner, string Next, HashSet<ushort> Types) exact = nsecs.FirstOrDefault(item =>
                    string.Equals(item.Owner, ancestor, StringComparison.Ordinal));
                if (exact.Owner == null) continue;
                if (IsDelegation(exact.Types)) return false;
                closestEncloser = ancestor;
                break;
            }
            if (closestEncloser == null) return false;
            string nextCloser = NextCloserName(canonicalName, closestEncloser);
            string wildcard = closestEncloser == "." ? "*." : "*." + closestEncloser;
            return nsecs.Any(item => Covers(item.Owner, item.Next, nextCloser)) &&
                   nsecs.Any(item => Covers(item.Owner, item.Next, wildcard));
        }

        private static bool ProvesNsec3NoData(DnsResponse response, string name, DnsRecordType type) {
            foreach (DnsWireResourceRecord record in DnsSecWire.Records(response)) {
                if (record.Type != DnsRecordType.NSEC3 || !TryReadNsec3(response.WireMessage, record, out Nsec3Value value)) continue;
                string ownerHash = FirstLabel(record.Name);
                string candidate = ToBase32Hex(HashName(name, value.Iterations, value.Salt));
                if (string.Equals(ownerHash, candidate, StringComparison.OrdinalIgnoreCase) &&
                    !value.Types.Contains((ushort)type) && !value.Types.Contains((ushort)DnsRecordType.CNAME)) return true;
                if (type == DnsRecordType.DS && value.OptOut &&
                    CoversHash(ownerHash, ToBase32Hex(value.NextHash), candidate)) return true;
            }
            return false;
        }

        private static bool ProvesNsec3NameError(DnsResponse response, string name) {
            var values = new List<(string OwnerHash, string Zone, Nsec3Value Value)>();
            foreach (DnsWireResourceRecord record in DnsSecWire.Records(response)) {
                if (record.Type != DnsRecordType.NSEC3 || !TryReadNsec3(response.WireMessage, record, out Nsec3Value value)) continue;
                string canonicalOwner = DnsWireNameCodec.Canonical(record.Name);
                int dot = canonicalOwner.IndexOf('.');
                if (dot <= 0) continue;
                values.Add((canonicalOwner.Substring(0, dot), canonicalOwner.Substring(dot + 1), value));
            }
            if (values.Count == 0) return false;
            Nsec3Value parameters = values[0].Value;
            if (values.Any(v => v.Value.HashAlgorithm != parameters.HashAlgorithm || v.Value.Iterations != parameters.Iterations ||
                                !v.Value.Salt.SequenceEqual(parameters.Salt) || v.Value.OptOut)) return false;

            string? closest = null;
            foreach (string candidate in Ancestors(name)) {
                string hash = ToBase32Hex(HashName(candidate, parameters.Iterations, parameters.Salt));
                (string OwnerHash, string Zone, Nsec3Value Value) exact = values.FirstOrDefault(v =>
                    string.Equals(v.OwnerHash, hash, StringComparison.OrdinalIgnoreCase));
                if (exact.OwnerHash == null) continue;
                if (IsDelegation(exact.Value.Types)) return false;
                closest = candidate;
                break;
            }
            if (closest == null) return false;
            string nextCloser = NextCloserName(name, closest);
            string wildcard = closest == "." ? "*." : "*." + closest;
            string nextHash = ToBase32Hex(HashName(nextCloser, parameters.Iterations, parameters.Salt));
            string wildcardHash = ToBase32Hex(HashName(wildcard, parameters.Iterations, parameters.Salt));
            return values.Any(v => CoversHash(v.OwnerHash, ToBase32Hex(v.Value.NextHash), nextHash)) &&
                   values.Any(v => CoversHash(v.OwnerHash, ToBase32Hex(v.Value.NextHash), wildcardHash));
        }

        private static bool IsDelegation(HashSet<ushort> types) {
            return types.Contains((ushort)DnsRecordType.NS) &&
                   !types.Contains((ushort)DnsRecordType.SOA);
        }

        private static bool TryReadNsec(byte[] message, DnsWireResourceRecord record, out string next, out HashSet<ushort> types) {
            next = string.Empty;
            types = new HashSet<ushort>();
            try {
                var reader = new DnsWireReader(message, record.RdataOffset, record.RdataOffset + record.RdataLength);
                next = DnsWireNameCodec.Canonical(reader.ReadName());
                ReadBitmap(reader, types);
                return true;
            } catch (DnsClientException) {
                return false;
            }
        }

        private static bool TryReadNsec3(byte[] message, DnsWireResourceRecord record, out Nsec3Value value) {
            value = default;
            try {
                var reader = new DnsWireReader(message, record.RdataOffset, record.RdataOffset + record.RdataLength);
                byte hashAlgorithm = reader.ReadByte();
                byte flags = reader.ReadByte();
                ushort iterations = reader.ReadUInt16();
                byte[] salt = reader.ReadBytes(reader.ReadByte());
                byte[] nextHash = reader.ReadBytes(reader.ReadByte());
                var types = new HashSet<ushort>();
                ReadBitmap(reader, types);
                if (hashAlgorithm != 1 || iterations > MaxSupportedNsec3Iterations || nextHash.Length == 0) return false;
                value = new Nsec3Value(hashAlgorithm, (flags & 1) != 0, iterations, salt, nextHash, types);
                return true;
            } catch (DnsClientException) {
                return false;
            }
        }

        private static void ReadBitmap(DnsWireReader reader, HashSet<ushort> types) {
            int previousWindow = -1;
            while (!reader.IsAtEnd) {
                int window = reader.ReadByte();
                int length = reader.ReadByte();
                if (window <= previousWindow || length < 1 || length > 32) throw new DnsClientException("Invalid NSEC type bitmap.");
                previousWindow = window;
                byte[] bitmap = reader.ReadBytes(length);
                if (bitmap[bitmap.Length - 1] == 0) throw new DnsClientException("Non-canonical NSEC type bitmap.");
                for (int octet = 0; octet < bitmap.Length; octet++) {
                    for (int bit = 0; bit < 8; bit++) {
                        if ((bitmap[octet] & (1 << (7 - bit))) != 0) types.Add((ushort)(window * 256 + octet * 8 + bit));
                    }
                }
            }
        }

        private static IEnumerable<string> Ancestors(string name) {
            string current = DnsWireNameCodec.Canonical(name);
            while (true) {
                yield return current;
                if (current == ".") yield break;
                int dot = current.IndexOf('.');
                current = dot < 0 || dot == current.Length - 1 ? "." : current.Substring(dot + 1);
            }
        }

        private static string NextCloserName(string name, string closest) {
            if (closest == ".") {
                string trimmed = name.TrimEnd('.');
                int last = trimmed.LastIndexOf('.');
                return (last < 0 ? trimmed : trimmed.Substring(last + 1)) + ".";
            }
            int suffix = name.Length - closest.Length;
            string prefix = name.Substring(0, suffix).TrimEnd('.');
            int dot = prefix.LastIndexOf('.');
            string label = dot < 0 ? prefix : prefix.Substring(dot + 1);
            return label + "." + closest;
        }

        private static bool Covers(string owner, string next, string candidate) {
            int ownerToCandidate = CompareNames(owner, candidate);
            int candidateToNext = CompareNames(candidate, next);
            int ownerToNext = CompareNames(owner, next);
            return ownerToNext < 0 ? ownerToCandidate < 0 && candidateToNext < 0 : ownerToCandidate < 0 || candidateToNext < 0;
        }

        private static int CompareNames(string left, string right) {
            byte[][] a = WireLabels(DnsWireNameCodec.ToCanonicalWire(left));
            byte[][] b = WireLabels(DnsWireNameCodec.ToCanonicalWire(right));
            int count = Math.Min(a.Length, b.Length);
            for (int i = 1; i <= count; i++) {
                int result = CompareLabel(a[a.Length - i], b[b.Length - i]);
                if (result != 0) return result;
            }
            return a.Length.CompareTo(b.Length);
        }

        private static byte[][] WireLabels(byte[] name) {
            var labels = new List<byte[]>();
            for (int offset = 0; offset < name.Length && name[offset] != 0;) {
                int length = name[offset++];
                var label = new byte[length];
                Buffer.BlockCopy(name, offset, label, 0, length);
                labels.Add(label);
                offset += length;
            }
            return labels.ToArray();
        }

        private static int CompareLabel(byte[] left, byte[] right) {
            int count = Math.Min(left.Length, right.Length);
            for (int i = 0; i < count; i++) {
                int result = left[i].CompareTo(right[i]);
                if (result != 0) return result;
            }
            return left.Length.CompareTo(right.Length);
        }

        private static bool CoversHash(string owner, string next, string candidate) {
            int ownerToNext = string.Compare(owner, next, StringComparison.OrdinalIgnoreCase);
            int ownerToCandidate = string.Compare(owner, candidate, StringComparison.OrdinalIgnoreCase);
            int candidateToNext = string.Compare(candidate, next, StringComparison.OrdinalIgnoreCase);
            return ownerToNext < 0 ? ownerToCandidate < 0 && candidateToNext < 0 : ownerToCandidate < 0 || candidateToNext < 0;
        }

        private static byte[] HashName(string name, ushort iterations, byte[] salt) {
            byte[] value = DnsWireNameCodec.ToCanonicalWire(name);
            using SHA1 sha1 = SHA1.Create();
            for (int i = 0; i <= iterations; i++) {
                var input = new byte[value.Length + salt.Length];
                Buffer.BlockCopy(value, 0, input, 0, value.Length);
                Buffer.BlockCopy(salt, 0, input, value.Length, salt.Length);
                value = sha1.ComputeHash(input);
            }
            return value;
        }

        private static string FirstLabel(string name) {
            string canonical = DnsWireNameCodec.Canonical(name);
            int dot = canonical.IndexOf('.');
            return dot < 0 ? canonical : canonical.Substring(0, dot);
        }

        private static string ToBase32Hex(byte[] value) {
            const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUV";
            var output = new System.Text.StringBuilder((value.Length * 8 + 4) / 5);
            int buffer = 0;
            int bits = 0;
            foreach (byte item in value) {
                buffer = (buffer << 8) | item;
                bits += 8;
                while (bits >= 5) {
                    bits -= 5;
                    output.Append(alphabet[(buffer >> bits) & 31]);
                }
            }
            if (bits > 0) output.Append(alphabet[(buffer << (5 - bits)) & 31]);
            return output.ToString();
        }

        private readonly struct Nsec3Value {
            internal Nsec3Value(byte hashAlgorithm, bool optOut, ushort iterations, byte[] salt, byte[] nextHash, HashSet<ushort> types) {
                HashAlgorithm = hashAlgorithm;
                OptOut = optOut;
                Iterations = iterations;
                Salt = salt;
                NextHash = nextHash;
                Types = types;
            }
            internal byte HashAlgorithm { get; }
            internal bool OptOut { get; }
            internal ushort Iterations { get; }
            internal byte[] Salt { get; }
            internal byte[] NextHash { get; }
            internal HashSet<ushort> Types { get; }
        }
    }
}
