using System;

namespace DnsClientX;

/// <summary>An authenticated optional-protocol response and the physical transport used by that exchange.</summary>
public sealed class DnsWireTransportResponse {
    /// <summary>Creates a response after the protocol provider has authenticated and decrypted its message.</summary>
    /// <param name="message">Complete DNS wire response without transport framing. The recipient owns this buffer.</param>
    /// <param name="transport">Physical transport used to receive this response, including any encrypted transport fallback.</param>
    public DnsWireTransportResponse(byte[] message, Transport transport) {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Transport = transport;
    }

    /// <summary>Gets the decrypted DNS wire response. The provider must not mutate it after returning it.</summary>
    public byte[] Message { get; }

    /// <summary>Gets the physical transport for this exchange.</summary>
    public Transport Transport { get; }
}
