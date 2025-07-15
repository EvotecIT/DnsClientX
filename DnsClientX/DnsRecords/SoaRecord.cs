namespace DnsClientX;
/// <summary>
/// Represents a SOA record with zone information.
/// </summary>
/// <remarks>
/// Start of Authority records describe global parameters for a DNS zone.
/// </remarks>
public sealed class SoaRecord {
    /// <summary>Gets the primary name server for the zone.</summary>
    public string PrimaryNameServer { get; }
    /// <summary>Gets the email address of the party responsible for the zone.</summary>
    public string ResponsiblePerson { get; }
    /// <summary>Gets the zone serial number.</summary>
    public uint Serial { get; }
    /// <summary>Gets the zone refresh interval in seconds.</summary>
    public uint Refresh { get; }
    /// <summary>Gets the retry interval in seconds.</summary>
    public uint Retry { get; }
    /// <summary>Gets the zone expiration time in seconds.</summary>
    public uint Expire { get; }
    /// <summary>Gets the minimum TTL for records in the zone.</summary>
    public uint Minimum { get; }

    /// <summary>Initializes a new instance of the <see cref="SoaRecord"/> class.</summary>
    public SoaRecord(string primaryNameServer, string responsiblePerson, uint serial, uint refresh, uint retry, uint expire, uint minimum) {
        PrimaryNameServer = primaryNameServer;
        ResponsiblePerson = responsiblePerson;
        Serial = serial;
        Refresh = refresh;
        Retry = retry;
        Expire = expire;
        Minimum = minimum;
    }
}

