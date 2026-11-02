using System;

namespace DnsClientX.DnsCrypt;

/// <summary>Creates DNSCrypt providers from trusted resolver and optional anonymizing relay stamps.</summary>
public sealed class DnsCryptTransportFactory : IDnsWireTransportFactory {
    /// <inheritdoc />
    public IDnsWireTransportProvider Create(string resolver, string? relay = null) =>
        new DnsCryptClient(DnsCryptOptions.FromStamp(resolver, relay));
}
