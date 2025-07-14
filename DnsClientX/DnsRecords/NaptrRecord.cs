namespace DnsClientX;

/// <summary>
/// Represents a NAPTR record used for dynamic service discovery.
/// </summary>
/// <remarks>
/// Naming Authority Pointer records are specified in <a href="https://www.rfc-editor.org/rfc/rfc2915">RFC 2915</a>.
/// </remarks>
public sealed class NaptrRecord {
    public ushort Order { get; }
    public ushort Preference { get; }
    public string Flags { get; }
    public string Service { get; }
    public string RegExp { get; }
    public string Replacement { get; }

    public NaptrRecord(ushort order, ushort preference, string flags, string service, string regExp, string replacement) {
        Order = order;
        Preference = preference;
        Flags = flags;
        Service = service;
        RegExp = regExp;
        Replacement = replacement;
    }
}
