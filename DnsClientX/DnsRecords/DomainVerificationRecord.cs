namespace DnsClientX;
using System;

/// <summary>
/// Represents a TXT record used for domain verification tokens.
/// </summary>
public sealed class DomainVerificationRecord {
    /// <summary>Gets the verification provider key.</summary>
    public string Provider { get; }

    /// <summary>Gets the verification token.</summary>
    public string Token { get; }

    /// <summary>Initializes a new instance of the <see cref="DomainVerificationRecord"/> class.</summary>
    /// <param name="provider">The provider key.</param>
    /// <param name="token">The verification token.</param>
    public DomainVerificationRecord(string provider, string token) {
        Provider = provider;
        Token = token;
    }

    /// <summary>Attempts to parse a domain verification TXT record.</summary>
    /// <param name="record">Raw TXT record.</param>
    /// <param name="result">Parsed record.</param>
    /// <returns><c>true</c> if parsing succeeded.</returns>
    public static bool TryParse(string record, out DomainVerificationRecord? result) {
        result = null;
        if (string.IsNullOrWhiteSpace(record)) {
            return false;
        }
        var parts = record.Split(new[] { '=' }, 2);
        if (parts.Length != 2) {
            return false;
        }
        if (parts[0].EndsWith("-verification", StringComparison.OrdinalIgnoreCase)) {
            result = new DomainVerificationRecord(parts[0], parts[1]);
            return true;
        }
        return false;
    }
}
