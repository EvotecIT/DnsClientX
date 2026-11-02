using System;

namespace DnsClientX.Odoh;

/// <summary>Creates ODoH providers from explicitly selected target and relay endpoints or stamps.</summary>
public sealed class OdohTransportFactory : IDnsWireTransportFactory {
    /// <inheritdoc />
    public IDnsWireTransportProvider Create(string resolver, string? relay = null) {
        if (string.IsNullOrWhiteSpace(relay)) { throw new ArgumentException("ODoH requires an explicit relay endpoint or stamp.", nameof(relay)); }
        return new OdohClient(OdohClientOptions.FromEndpoints(relay!, resolver));
    }
}
