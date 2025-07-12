namespace DnsClientX;
/// <summary>
/// Represents an NS record containing a name server host.
/// </summary>
public sealed class NsRecord {
    /// <summary>Gets the host name.</summary>
    public string Host { get; }

    /// <summary>Initializes a new instance of the <see cref="NsRecord"/> class.</summary>
    /// <param name="host">Name server host.</param>
    public NsRecord(string host) => Host = host;
}

