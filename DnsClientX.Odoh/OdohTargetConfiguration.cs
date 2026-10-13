using System;
using System.IO;
using System.Text;

namespace DnsClientX.Odoh;

/// <summary>An immutable, expiring RFC 9230 target configuration for the mandatory HPKE cipher suite.</summary>
public sealed class OdohTargetConfiguration {
    private readonly byte[] _contents;
    private readonly byte[] _publicKey;
    private readonly byte[] _keyId;

    private OdohTargetConfiguration(byte[] contents, byte[] publicKey, DateTimeOffset expiresAt) {
        _contents = contents;
        _publicKey = publicKey;
        ExpiresAt = expiresAt;
        byte[] prk = HpkeX25519.Extract(Array.Empty<byte>(), _contents);
        try {
            _keyId = HpkeX25519.Expand(prk, Encoding.ASCII.GetBytes("odoh key id"), 32);
        } finally {
            Array.Clear(prk, 0, prk.Length);
        }
    }

    /// <summary>Gets the time after which this key must be reacquired.</summary>
    public DateTimeOffset ExpiresAt { get; }
    /// <summary>Gets a copy of the target's X25519 public key.</summary>
    public byte[] PublicKey => (byte[])_publicKey.Clone();
    /// <summary>Gets a copy of the RFC 9230 key identifier.</summary>
    public byte[] KeyId => (byte[])_keyId.Clone();
    internal byte[] PublicKeyBytes => _publicKey;
    internal byte[] KeyIdBytes => _keyId;

    /// <summary>Parses an ObliviousDoHConfigs list and chooses its first supported configuration.</summary>
    /// <param name="encodedConfigurations">The complete RFC 9230 length-prefixed configuration list.</param>
    /// <param name="expiresAt">An expiry established by authenticated configuration acquisition.</param>
    /// <returns>The first version 1 X25519/HKDF-SHA256/AES-128-GCM configuration.</returns>
    /// <exception cref="InvalidDataException">The list is malformed or a supported key is invalid.</exception>
    /// <exception cref="NotSupportedException">No configuration uses the supported cipher suite.</exception>
    public static OdohTargetConfiguration Parse(byte[] encodedConfigurations, DateTimeOffset expiresAt) {
        if (encodedConfigurations == null) {
            throw new ArgumentNullException(nameof(encodedConfigurations));
        }
        int offset = 0;
        int length = OdohEncoding.ReadUInt16(encodedConfigurations, ref offset, encodedConfigurations.Length);
        if (length == 0 || length != encodedConfigurations.Length - offset) {
            throw new InvalidDataException("Invalid ODoH configuration list length.");
        }
        OdohTargetConfiguration? selected = null;
        while (offset < encodedConfigurations.Length) {
            int version = OdohEncoding.ReadUInt16(encodedConfigurations, ref offset, encodedConfigurations.Length);
            byte[] contents = OdohEncoding.ReadVector(encodedConfigurations, ref offset, encodedConfigurations.Length);
            if (version != 1) {
                continue;
            }
            int innerOffset = 0;
            int kem = OdohEncoding.ReadUInt16(contents, ref innerOffset, contents.Length);
            int kdf = OdohEncoding.ReadUInt16(contents, ref innerOffset, contents.Length);
            int aead = OdohEncoding.ReadUInt16(contents, ref innerOffset, contents.Length);
            byte[] publicKey = OdohEncoding.ReadVector(contents, ref innerOffset, contents.Length);
            if (publicKey.Length == 0 || innerOffset != contents.Length) {
                throw new InvalidDataException("Invalid ODoH configuration contents.");
            }
            if (kem != 0x0020 || kdf != 0x0001 || aead != 0x0001) {
                continue;
            }
            HpkeX25519.ValidatePublicKey(publicKey);
            if (selected == null) {
                selected = new OdohTargetConfiguration(contents, publicKey, expiresAt);
            }
        }
        return selected ?? throw new NotSupportedException("The target offers no version 1 X25519/HKDF-SHA256/AES-128-GCM ODoH configuration.");
    }
}
