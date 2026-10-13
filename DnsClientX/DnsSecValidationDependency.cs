using System;

namespace DnsClientX;

/// <summary>Material actually fetched while validating a response. This describes observations, not a complete trust graph.</summary>
public sealed class DnsSecValidationDependency {
    internal DnsSecValidationDependency(string name, DnsRecordType type, DnsResponse response,
        DateTimeOffset? reusableUntilUtc) {
        Name = name;
        Type = type;
        ResponseSource = response.ResponseSource;
        Status = response.Status;
        AnswerCount = response.Answers.Length;
        AuthorityCount = response.Authorities.Length;
        ReusableUntilUtc = reusableUntilUtc;
    }
    /// <summary>Gets the queried owner name.</summary>
    public string Name { get; }
    /// <summary>Gets the queried record type.</summary>
    public DnsRecordType Type { get; }
    /// <summary>Gets whether material came from the network or the client's material cache.</summary>
    public DnsResponseSource ResponseSource { get; }
    /// <summary>Gets the material response status.</summary>
    public DnsResponseCode Status { get; }
    /// <summary>Gets the observed answer count.</summary>
    public int AnswerCount { get; }
    /// <summary>Gets the observed authority count.</summary>
    public int AuthorityCount { get; }
    /// <summary>Gets the bounded reuse deadline, or null if the validation did not qualify this material for reuse.</summary>
    public DateTimeOffset? ReusableUntilUtc { get; }
}
