namespace DnsClientX;
/// <summary>
/// Represents a DNS record type without a dedicated parser.
/// </summary>
/// <remarks>
/// This class simply exposes the raw data string for unknown record types.
/// </remarks>
public sealed class UnknownRecord {
    /// <summary>Gets the raw record data.</summary>
    public string Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownRecord"/> class.
    /// </summary>
    /// <param name="data">The raw record data.</param>
    public UnknownRecord(string data) => Data = data;
}

