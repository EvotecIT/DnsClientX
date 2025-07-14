namespace DnsClientX;
/// <summary>
/// Represents an MX record containing a mail exchange server.
/// </summary>
/// <remarks>
/// Mail exchange records route email according to <a href="https://www.rfc-editor.org/rfc/rfc5321">RFC 5321</a>.
/// </remarks>
public sealed class MxRecord {
    /// <summary>Gets the preference value.</summary>
    public int Preference { get; }
    /// <summary>Gets the mail server host.</summary>
    public string Exchange { get; }
    /// <summary>Initializes a new instance of the <see cref="MxRecord"/> class.</summary>
    /// <param name="preference">Preference value.</param>
    /// <param name="exchange">Mail server host.</param>
    public MxRecord(int preference, string exchange) {
        Preference = preference;
        Exchange = exchange;
    }
}
