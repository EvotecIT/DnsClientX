using System;
using System.IO;
using System.Text;

namespace DnsClientX.Odoh;

/// <summary>Binds the RFC 9230 response exporter and salt to one serialized query.</summary>
internal sealed class OdohQueryContext : IDisposable {
    private readonly byte[] _plaintext;
    private readonly byte[] _responseSecret;
    private bool _disposed;

    private OdohQueryContext(byte[] plaintext, byte[] responseSecret, byte[] message) {
        _plaintext = plaintext;
        _responseSecret = responseSecret;
        Message = message;
    }

    internal byte[] Message { get; }

    internal static OdohQueryContext Create(OdohTargetConfiguration configuration, byte[] query, int paddingBlockSize, byte[]? ephemeralPrivateKey = null) {
        byte[] plaintext = OdohEncoding.EncodePlaintext(query, paddingBlockSize);
        byte[]? responseSecret = null;
        try {
            using (HpkeContext hpke = HpkeX25519.CreateSender(configuration.PublicKeyBytes, Encoding.ASCII.GetBytes("odoh query"), ephemeralPrivateKey)) {
                byte[] aad = OdohEncoding.Concat(new byte[] { 1 }, OdohEncoding.Vector(configuration.KeyIdBytes));
                responseSecret = hpke.Export(Encoding.ASCII.GetBytes("odoh response"), 16);
                byte[] ciphertext = hpke.Seal(aad, plaintext);
                byte[] message = OdohEncoding.EncodeMessage(1, configuration.KeyIdBytes, OdohEncoding.Concat(hpke.Enc, ciphertext));
                return new OdohQueryContext(plaintext, responseSecret, message);
            }
        } catch {
            HpkeX25519.Clear(plaintext);
            HpkeX25519.Clear(responseSecret);
            throw;
        }
    }

    internal byte[] OpenResponse(byte[] response) {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(OdohQueryContext));
        }
        if (response.Length == 0 || response[0] != 2) {
            throw new InvalidDataException("An ODoH response must have message type 2.");
        }
        int offset = 1;
        byte[] responseNonce = OdohEncoding.ReadVector(response, ref offset, response.Length);
        byte[] ciphertext = OdohEncoding.ReadVector(response, ref offset, response.Length);
        if (offset != response.Length || responseNonce.Length != 16 || ciphertext.Length < 16) {
            throw new InvalidDataException("Invalid ODoH response framing or nonce length.");
        }
        byte[] aad = OdohEncoding.Concat(new byte[] { 2 }, OdohEncoding.Vector(responseNonce));
        byte[] salt = OdohEncoding.Concat(_plaintext, OdohEncoding.Vector(responseNonce));
        byte[]? prk = null;
        byte[]? key = null;
        byte[]? nonce = null;
        byte[]? plaintext = null;
        try {
            prk = HpkeX25519.Extract(salt, _responseSecret);
            key = HpkeX25519.Expand(prk, Encoding.ASCII.GetBytes("odoh key"), 16);
            nonce = HpkeX25519.Expand(prk, Encoding.ASCII.GetBytes("odoh nonce"), 12);
            plaintext = HpkeX25519.Aead(false, key, nonce, aad, ciphertext);
            return OdohEncoding.DecodePlaintext(plaintext);
        } finally {
            HpkeX25519.Clear(salt);
            HpkeX25519.Clear(prk);
            HpkeX25519.Clear(key);
            HpkeX25519.Clear(nonce);
            HpkeX25519.Clear(plaintext);
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        HpkeX25519.Clear(_plaintext);
        HpkeX25519.Clear(_responseSecret);
    }
}
