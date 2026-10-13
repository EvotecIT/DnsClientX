using System;

namespace DnsClientX;

/// <summary>Identifies an RFC 2136 update operation.</summary>
public enum DnsUpdateAction {
    /// <summary>Add an RDATA value.</summary>
    Add,
    /// <summary>Delete every value of one record type.</summary>
    DeleteRrset,
    /// <summary>Delete one exact RDATA value.</summary>
    DeleteValue,
    /// <summary>Delete every RRset at one owner name.</summary>
    DeleteName
}

/// <summary>One immutable operation in an atomic DNS UPDATE message.</summary>
public sealed class DnsUpdateOperation {
    private DnsUpdateOperation(string name, DnsRecordType type, DnsUpdateAction action, string? data, int ttl) {
        if (string.IsNullOrWhiteSpace(name)) { throw new ArgumentException("An owner name is required.", nameof(name)); }
        if (ttl < 0) { throw new ArgumentOutOfRangeException(nameof(ttl)); }
        DnsUpdateMessage.ValidateRecordType(type);
        Name = name; RecordType = type; Action = action; Data = data; Ttl = ttl;
    }
    /// <summary>Gets the record owner.</summary>
    public string Name { get; }
    /// <summary>Gets the record type.</summary>
    public DnsRecordType RecordType { get; }
    /// <summary>Gets the mutation.</summary>
    public DnsUpdateAction Action { get; }
    /// <summary>Gets typed presentation RDATA when the operation includes a value.</summary>
    public string? Data { get; }
    /// <summary>Gets the TTL used by an addition.</summary>
    public int Ttl { get; }
    /// <summary>Creates an addition using the core's typed RDATA serializer.</summary>
    public static DnsUpdateOperation Add(string name, DnsRecordType type, string data, int ttl = 300) =>
        new(name, type, DnsUpdateAction.Add, data ?? throw new ArgumentNullException(nameof(data)), ttl);
    /// <summary>Creates deletion of an entire RRset.</summary>
    public static DnsUpdateOperation DeleteRrset(string name, DnsRecordType type) => new(name, type, DnsUpdateAction.DeleteRrset, null, 0);
    /// <summary>Creates deletion of one exact RDATA value.</summary>
    public static DnsUpdateOperation DeleteValue(string name, DnsRecordType type, string data) =>
        new(name, type, DnsUpdateAction.DeleteValue, data ?? throw new ArgumentNullException(nameof(data)), 0);
    /// <summary>Creates deletion of all RRsets at an owner name.</summary>
    public static DnsUpdateOperation DeleteName(string name) => new(name, DnsRecordType.ANY, DnsUpdateAction.DeleteName, null, 0);
}
