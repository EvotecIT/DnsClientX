namespace DnsClientX;

/// <summary>A parsed DNS record together with its owner, class, remaining TTL and wire evidence.</summary>
/// <typeparam name="TRecord">The parsed payload type.</typeparam>
public sealed class DnsTypedRecord<TRecord> where TRecord : class {
    internal DnsTypedRecord(DnsAnswer record, TRecord value) {
        Record = record;
        Value = value;
    }

    /// <summary>Gets the source record, including raw RDATA when available.</summary>
    public DnsAnswer Record { get; }
    /// <summary>Gets the parsed payload.</summary>
    public TRecord Value { get; }
    /// <summary>Gets the record owner.</summary>
    public string Name => Record.Name;
    /// <summary>Gets the DNS record type.</summary>
    public DnsRecordType Type => Record.Type;
    /// <summary>Gets the remaining lifetime in seconds.</summary>
    public int TTL => Record.TTL;
    /// <summary>Gets the DNS class.</summary>
    public ushort? Class => Record.Class;
}
