using System;
using System.Collections.Generic;

namespace DnsClientX {
    internal static partial class DnsSecProof {
        // This predicate is called only with records authenticated by one in-bailiwick signer.
        internal static bool ProvesCompactNameError(DnsResponse response, string name, out bool hasNxName) {
            hasNxName = false;
            string canonical = DnsWireNameCodec.Canonical(name);
            bool proved = false;
            foreach (DnsWireResourceRecord record in DnsSecWire.Records(response)) {
                if (record.Type == DnsRecordType.NSEC && TryReadNsec(response.WireMessage, record, out _, out HashSet<ushort> types)) {
                    if (!types.Contains((ushort)DnsRecordType.NXNAME)) continue;
                    hasNxName = true;
                    proved |= string.Equals(DnsWireNameCodec.Canonical(record.Name), canonical, StringComparison.Ordinal)
                        && types.SetEquals(new[] { (ushort)DnsRecordType.RRSIG, (ushort)DnsRecordType.NSEC, (ushort)DnsRecordType.NXNAME });
                } else if (record.Type == DnsRecordType.NSEC3 && TryReadNsec3(response.WireMessage, record, out Nsec3Value value)) {
                    if (!value.Types.Contains((ushort)DnsRecordType.NXNAME)) continue;
                    hasNxName = true;
                    proved |= value.NextHash.Length == 20 && value.Types.Count == 1 && string.Equals(FirstLabel(record.Name),
                        ToBase32Hex(HashName(canonical, value.Iterations, value.Salt)), StringComparison.OrdinalIgnoreCase);
                }
            }
            return proved;
        }
    }
}
