using System;
using System.Collections.Generic;
using System.Net;

namespace DnsClientX;
/// <summary>Identifies a continuous DNS-SD browse change.</summary>
public enum DnsServiceBrowseChange {
    /// <summary>An instance with a PTR and SRV record became available.</summary>
    Added,
    /// <summary>A live instance's endpoint, metadata, or address set changed.</summary>
    Updated,
    /// <summary>A goodbye withdrew the instance after the RFC 6762 rescue interval.</summary>
    Removed,
    /// <summary>The instance's PTR or SRV evidence expired or was flushed.</summary>
    Expired
}

/// <summary>Contains an independent snapshot of one DNS-SD instance change.</summary>
public sealed class DnsServiceBrowseEvent {
    /// <summary>Gets the change kind.</summary>
    public DnsServiceBrowseChange Change { get; }
    /// <summary>Gets the service snapshot at the change; removals retain the last live snapshot.</summary>
    public DnsService Service { get; }
    /// <summary>Gets independent IP address snapshots for the SRV target.</summary>
    public IReadOnlyList<IPAddress> Addresses { get; }
    /// <summary>Gets when the change was observed.</summary>
    public DateTimeOffset ObservedAt { get; }

    internal DnsServiceBrowseEvent(DnsServiceBrowseChange change, DnsService service, IPAddress[] addresses, DateTimeOffset observedAt) {
        Change = change;
        Service = service;
        Addresses = Array.AsReadOnly(addresses);
        ObservedAt = observedAt;
    }
}

/// <summary>Bounds a persistent multicast DNS-SD browse session.</summary>
public sealed class DnsServiceBrowseOptions {
    /// <summary>Gets or sets the maximum live resource-record cache entries. Exceeding the bound terminates the session explicitly.</summary>
    public int MaximumRecords { get; set; } = 512;
    /// <summary>Gets or sets the maximum live service instances. Exceeding the bound terminates the session explicitly.</summary>
    public int MaximumServices { get; set; } = 128;
    /// <summary>Gets or sets the maximum pending change events. A slow consumer applies backpressure.</summary>
    public int EventBufferCapacity { get; set; } = 32;
}
