using System;

namespace DnsClientX.Odoh;

/// <summary>A single-use HPKE sender context. ODoH creates a new context for every query.</summary>
internal sealed class HpkeContext : IDisposable {
    private readonly byte[] _key;
    private readonly byte[] _baseNonce;
    private readonly byte[] _exporterSecret;
    private bool _sealed;
    private bool _disposed;

    internal HpkeContext(byte[] enc, byte[] key, byte[] baseNonce, byte[] exporterSecret) {
        Enc = enc;
        _key = key;
        _baseNonce = baseNonce;
        _exporterSecret = exporterSecret;
    }

    internal byte[] Enc { get; }

    internal byte[] Seal(byte[] aad, byte[] plaintext) {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(HpkeContext));
        }
        if (_sealed) {
            throw new InvalidOperationException("An ODoH HPKE context can seal only one query.");
        }
        _sealed = true;
        return HpkeX25519.Aead(true, _key, _baseNonce, aad, plaintext);
    }

    internal byte[] Export(byte[] context, int length) {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(HpkeContext));
        }
        return HpkeX25519.Export(_exporterSecret, context, length);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        HpkeX25519.Clear(_key);
        HpkeX25519.Clear(_baseNonce);
        HpkeX25519.Clear(_exporterSecret);
    }
}
