namespace DnsClientX;
/// <summary>
/// Represents an SRV record specifying host and port for a service.
/// </summary>
/// <remarks>
/// Defined in <a href="https://www.rfc-editor.org/rfc/rfc2782">RFC 2782</a>.
/// </remarks>
public sealed class SrvRecord {
    /// <summary>Gets the priority of the target host.</summary>
    public ushort Priority { get; }
    /// <summary>Gets the weight used to select between records with the same priority.</summary>
    public ushort Weight { get; }
    /// <summary>Gets the port on the target host of the service.</summary>
    public ushort Port { get; }
    /// <summary>Gets the domain name of the target host.</summary>
    public string Target { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SrvRecord"/> class.
    /// </summary>
    /// <param name="priority">Priority of the target host.</param>
    /// <param name="weight">Relative weight for records with the same priority.</param>
    /// <param name="port">Service port.</param>
    /// <param name="target">Domain name of the target host.</param>
    public SrvRecord(ushort priority, ushort weight, ushort port, string target) {
        Priority = priority;
        Weight = weight;
        Port = port;
        Target = target;
    }
}

