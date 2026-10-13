using System;
using System.Collections.Generic;
using System.IO;

namespace DnsClientX;

internal static partial class DnsUpdateMessage {
    private readonly struct UpdateEntry {
        internal UpdateEntry(string name, DnsRecordType type, ushort recordClass, uint ttl, byte[] data) {
            Name = name; Type = type; Class = recordClass; Ttl = ttl; Data = data;
        }
        internal string Name { get; }
        internal DnsRecordType Type { get; }
        internal ushort Class { get; }
        internal uint Ttl { get; }
        internal byte[] Data { get; }
    }

    internal static DnsUpdateRequestMessage CreateBatchMessage(DnsUpdateBatch batch, TsigKey? key = null) =>
        SerializeEntries(batch.Zone, PrerequisiteEntries(batch), OperationEntries(batch), key);

    private static IEnumerable<UpdateEntry> PrerequisiteEntries(DnsUpdateBatch batch) {
        var compared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DnsUpdatePrerequisite condition in batch.Prerequisites) {
            switch (condition.Kind) {
                case DnsUpdatePrerequisiteKind.NameExists:
                    yield return new UpdateEntry(condition.Name, DnsRecordType.ANY, ClassAny, 0, Array.Empty<byte>());
                    break;
                case DnsUpdatePrerequisiteKind.NameAbsent:
                    yield return new UpdateEntry(condition.Name, DnsRecordType.ANY, ClassNone, 0, Array.Empty<byte>());
                    break;
                case DnsUpdatePrerequisiteKind.RrsetExists:
                case DnsUpdatePrerequisiteKind.RrsetAbsent:
                    ValidateBatchRrsetType(condition.RecordType);
                    yield return new UpdateEntry(condition.Name, condition.RecordType,
                        condition.Kind == DnsUpdatePrerequisiteKind.RrsetExists ? ClassAny : ClassNone, 0, Array.Empty<byte>());
                    break;
                case DnsUpdatePrerequisiteKind.RrsetEquals:
                    ValidateBatchRrsetType(condition.RecordType);
                    string identity = DnsWireNameCodec.Canonical(condition.Name) + ":" + (ushort)condition.RecordType;
                    if (!compared.Add(identity)) { throw new ArgumentException("Specify one complete value prerequisite per owner/type."); }
                    foreach (string value in condition.Values) {
                        yield return new UpdateEntry(condition.Name, condition.RecordType, ClassIn, 0, BuildRdata(condition.RecordType, value));
                    }
                    break;
                default:
                    throw new ArgumentException("Unknown UPDATE prerequisite kind.");
            }
        }
    }

    private static IEnumerable<UpdateEntry> OperationEntries(DnsUpdateBatch batch) {
        foreach (DnsUpdateOperation operation in batch.Operations) {
            if (operation.Action != DnsUpdateAction.DeleteName) { ValidateBatchRrsetType(operation.RecordType); }
            yield return operation.Action switch {
                DnsUpdateAction.Add => new UpdateEntry(operation.Name, operation.RecordType, ClassIn,
                    checked((uint)operation.Ttl), BuildRdata(operation.RecordType, operation.Data!)),
                DnsUpdateAction.DeleteValue => new UpdateEntry(operation.Name, operation.RecordType, ClassNone, 0,
                    BuildRdata(operation.RecordType, operation.Data!)),
                DnsUpdateAction.DeleteRrset => new UpdateEntry(operation.Name, operation.RecordType, ClassAny, 0, Array.Empty<byte>()),
                DnsUpdateAction.DeleteName => new UpdateEntry(operation.Name, DnsRecordType.ANY, ClassAny, 0, Array.Empty<byte>()),
                _ => throw new ArgumentException("Unknown UPDATE operation.")
            };
        }
    }

    private static void ValidateBatchRrsetType(DnsRecordType type) {
        ValidateRecordType(type);
        ushort value = (ushort)type;
        if (value == 0 || value == 41 || value >= 249 && value <= 255) {
            throw new ArgumentException("A concrete resource-record type is required for this operation.", nameof(type));
        }
    }

    private static DnsUpdateRequestMessage SerializeEntries(string zone, IEnumerable<UpdateEntry> prerequisites,
        IEnumerable<UpdateEntry> operations, TsigKey? key) {
        zone = DnsWireNameCodec.Normalize(zone);
        ushort transactionId = CreateTransactionId();
        using var stream = new MemoryStream();
        WriteUInt16(stream, transactionId); WriteUInt16(stream, 0x2800);
        WriteUInt16(stream, 1); WriteUInt16(stream, 0); WriteUInt16(stream, 0); WriteUInt16(stream, 0);
        WriteName(stream, zone); WriteUInt16(stream, (ushort)DnsRecordType.SOA); WriteUInt16(stream, ClassIn);
        ushort prerequisiteCount = WriteEntries(stream, zone, prerequisites);
        ushort operationCount = WriteEntries(stream, zone, operations);
        stream.Position = 6; WriteUInt16(stream, prerequisiteCount); WriteUInt16(stream, operationCount);
        byte[] unsigned = stream.ToArray();
        if (key == null) { return new DnsUpdateRequestMessage(transactionId, zone, unsigned, Array.Empty<byte>()); }
        DnsTsigSignedMessage signed = DnsTsig.Sign(unsigned, transactionId, key);
        if (signed.WireData.Length > ushort.MaxValue) { throw new ArgumentException("The signed UPDATE exceeds the DNS message size limit."); }
        return new DnsUpdateRequestMessage(transactionId, zone, signed.WireData, signed.Mac);
    }

    private static ushort WriteEntries(Stream stream, string zone, IEnumerable<UpdateEntry> entries) {
        ushort count = 0;
        foreach (UpdateEntry entry in entries) {
            string name = DnsWireNameCodec.Normalize(entry.Name);
            if (zone != "." && !string.Equals(name, zone, StringComparison.OrdinalIgnoreCase)
                && !name.EndsWith("." + zone, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"Record name {name} is outside update zone {zone}.");
            }
            byte[] owner = DnsWireNameCodec.ToCanonicalWire(name);
            if (entry.Data.Length > ushort.MaxValue || stream.Length + owner.Length + 10 + entry.Data.Length > ushort.MaxValue) {
                throw new ArgumentException("The atomic UPDATE exceeds the DNS message size limit; it cannot be split safely.");
            }
            count = checked((ushort)(count + 1));
            stream.Write(owner, 0, owner.Length); WriteUInt16(stream, (ushort)entry.Type);
            WriteUInt16(stream, entry.Class); WriteUInt32(stream, entry.Ttl);
            WriteUInt16(stream, (ushort)entry.Data.Length); stream.Write(entry.Data, 0, entry.Data.Length);
        }
        return count;
    }
}
