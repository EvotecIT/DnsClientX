using System;

namespace DnsClientX;

/// <summary>Query behavior for a reusable client. Resolver and transport settings belong to the client configuration.</summary>
public sealed class DnsResolveOptions {
    /// <summary>Gets or sets whether DNSSEC records are requested.</summary>
    public bool RequestDnsSec { get; set; }
    /// <summary>Gets or sets whether responses are validated locally.</summary>
    public bool ValidateDnsSec { get; set; }
    /// <summary>Gets or sets whether the complete answer section is retained.</summary>
    public bool ReturnAllTypes { get; set; }
    /// <summary>Gets or sets whether transient failures are retried.</summary>
    public bool RetryOnTransient { get; set; } = true;
    /// <summary>Gets or sets the retry limit used by the client.</summary>
    public int MaxRetries { get; set; } = 3;
    /// <summary>Gets or sets the retry delay in milliseconds.</summary>
    public int RetryDelayMs { get; set; } = 200;
    /// <summary>Gets or sets whether parsed payload arrays are populated.</summary>
    public bool TypedRecords { get; set; }
    /// <summary>Gets or sets whether TXT records are parsed into specialized payloads.</summary>
    public bool ParseTypedTxtRecords { get; set; }

    /// <summary>Rejects invalid retry settings before sending a query.</summary>
    public void Validate() {
        if (MaxRetries < 0) throw new ArgumentOutOfRangeException(nameof(MaxRetries));
        if (RetryDelayMs < 0) throw new ArgumentOutOfRangeException(nameof(RetryDelayMs));
    }

    internal DnsResolveOptions Snapshot() {
        Validate();
        return (DnsResolveOptions)MemberwiseClone();
    }
}
