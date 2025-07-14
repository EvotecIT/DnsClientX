namespace DnsClientX;
/// <summary>
/// Represents an NS record containing a name server host.
/// </summary>
/// <remarks>
/// NS records delegate authority to another name server, see <a href="https://www.rfc-editor.org/rfc/rfc1035">RFC 1035</a>.
/// </remarks>
public sealed class NsRecord {
    /// <summary>Gets the host name.</summary>
    public string Host { get; }

    /// <summary>Initializes a new instance of the <see cref="NsRecord"/> class.</summary>
    /// <param name="host">Name server host.</param>
    public NsRecord(string host) => Host = host;
}

