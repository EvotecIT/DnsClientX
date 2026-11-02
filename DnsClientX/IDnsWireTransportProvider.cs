using System;

namespace DnsClientX;

/// <summary>A caller-owned optional wire transport that supplies its complete client configuration.</summary>
public interface IDnsWireTransportProvider : IDnsWireTransport, IDisposable {
    /// <summary>Creates a configuration borrowing this provider. Dispose clients before disposing the provider.</summary>
    Configuration CreateConfiguration();
}

/// <summary>Creates an optional transport from an explicitly selected resolver and relay.</summary>
public interface IDnsWireTransportFactory {
    /// <summary>Creates a caller-owned provider; DNSCrypt requires a trusted resolver stamp, ODoH requires a target and relay.</summary>
    IDnsWireTransportProvider Create(string resolver, string? relay = null);
}
