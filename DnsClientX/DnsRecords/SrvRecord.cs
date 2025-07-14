namespace DnsClientX;
/// <summary>
/// Represents an SRV record specifying host and port for a service.
/// </summary>
/// <remarks>
/// Defined in <a href="https://www.rfc-editor.org/rfc/rfc2782">RFC 2782</a>.
/// </remarks>
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

