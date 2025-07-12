namespace DnsClientX;
/// <summary>
/// Represents a DNS record type without a dedicated parser.
/// </summary>
public sealed class UnknownRecord {
    /// <summary>Gets the raw record data.</summary>
    public string Data { get; }

    public UnknownRecord(string data) => Data = data;
}

