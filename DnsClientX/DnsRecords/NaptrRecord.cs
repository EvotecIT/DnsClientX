namespace DnsClientX;

/// <summary>
/// Represents a NAPTR record used for dynamic service discovery.
/// </summary>
/// <remarks>
/// Naming Authority Pointer records are specified in <a href="https://www.rfc-editor.org/rfc/rfc2915">RFC 2915</a>.
/// </remarks>
public sealed class NaptrRecord {
    /// <summary>Gets the order in which records should be processed.</summary>
    public ushort Order { get; }
    /// <summary>Gets the preference for services with the same order.</summary>
    public ushort Preference { get; }
    /// <summary>Gets the control flags for the lookup.</summary>
    public string Flags { get; }
    /// <summary>Gets the service parameters.</summary>
    public string Service { get; }
    /// <summary>Gets the regular expression used for rewriting.</summary>
    public string RegExp { get; }
    /// <summary>Gets the replacement domain name.</summary>
    public string Replacement { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NaptrRecord"/> class.
    /// </summary>
    /// <param name="order">Order of this record.</param>
    /// <param name="preference">Preference among equal-order records.</param>
    /// <param name="flags">Control flags.</param>
    /// <param name="service">Service parameters.</param>
    /// <param name="regExp">Rewrite expression.</param>
    /// <param name="replacement">Replacement domain.</param>
    public NaptrRecord(ushort order, ushort preference, string flags, string service, string regExp, string replacement) {
        Order = order;
        Preference = preference;
        Flags = flags;
        Service = service;
        RegExp = regExp;
        Replacement = replacement;
    }
}
