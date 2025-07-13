namespace DnsClientX;
/// <summary>
/// Represents an SRV record specifying host and port for a service.
/// </summary>
public sealed class SrvRecord {
    public ushort Priority { get; }
    public ushort Weight { get; }
    public ushort Port { get; }
    public string Target { get; }

    /// <summary>Initializes a new instance of the <see cref="SrvRecord"/> class.</summary>
    public SrvRecord(ushort priority, ushort weight, ushort port, string target) {
        Priority = priority;
        Weight = weight;
        Port = port;
        Target = target;
    }
}

