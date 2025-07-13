namespace DnsClientX;
/// <summary>
/// Represents a SOA record with zone information.
/// </summary>
public sealed class SoaRecord {
    public string PrimaryNameServer { get; }
    public string ResponsiblePerson { get; }
    public uint Serial { get; }
    public uint Refresh { get; }
    public uint Retry { get; }
    public uint Expire { get; }
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

