using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC.Rfc7748;

namespace DnsClientX.Odoh;

/// <summary>RFC 9180 base-mode DHKEM(X25519, HKDF-SHA256), HKDF-SHA256, AES-128-GCM.
/// The schedule is protocol composition over Bouncy Castle X25519/GCM and platform HMAC primitives.</summary>
internal static class HpkeX25519 {
    private static readonly byte[] KemSuite = { 0x4b, 0x45, 0x4d, 0x00, 0x20 };
    private static readonly byte[] HpkeSuite = { 0x48, 0x50, 0x4b, 0x45, 0x00, 0x20, 0x00, 0x01, 0x00, 0x01 };
    private static readonly byte[] Version = Encoding.ASCII.GetBytes("HPKE-v1");

    internal static byte[] RandomBytes(int length) {
        var value = new byte[length];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create()) {
            random.GetBytes(value);
        }
        return value;
    }

    internal static void ValidatePublicKey(byte[] publicKey) {
        if (publicKey.Length != 32) {
            throw new InvalidDataException("The ODoH X25519 public key must contain 32 bytes.");
        }
        // RFC 9180 requires rejecting X25519 inputs yielding an all-zero DH result.
        // A fixed validation scalar is sufficient to reject the small-order points.
        var scalar = new byte[32];
        scalar[0] = 8;
        scalar[31] = 64;
        var agreement = new byte[32];
        try {
            if (!X25519.CalculateAgreement(scalar, 0, publicKey, 0, agreement, 0)) {
                throw new InvalidDataException("The ODoH target has an invalid X25519 public key.");
            }
        } finally {
            Array.Clear(scalar, 0, scalar.Length);
            Array.Clear(agreement, 0, agreement.Length);
        }
    }

    /// <summary>Encapsulates a fresh sender context. A caller-supplied scalar is useful for deterministic primitive qualification.</summary>
    internal static HpkeContext CreateSender(byte[] publicKey, byte[] info, byte[]? ephemeralPrivateKey = null) {
        if (publicKey.Length != 32) {
            throw new InvalidDataException("The ODoH X25519 public key must contain 32 bytes.");
        }
        byte[] ephemeral = ephemeralPrivateKey == null ? RandomBytes(32) : (byte[])ephemeralPrivateKey.Clone();
        if (ephemeral.Length != 32) {
            Array.Clear(ephemeral, 0, ephemeral.Length);
            throw new ArgumentException("An X25519 private key must contain 32 bytes.", nameof(ephemeralPrivateKey));
        }
        var enc = new byte[32];
        var dh = new byte[32];
        byte[]? eaePrk = null;
        byte[]? sharedSecret = null;
        byte[]? secret = null;
        try {
            X25519.GeneratePublicKey(ephemeral, 0, enc, 0);
            if (!X25519.CalculateAgreement(ephemeral, 0, publicKey, 0, dh, 0)) {
                throw new CryptographicException("The ODoH target has an invalid X25519 public key.");
            }
            eaePrk = LabeledExtract(KemSuite, Array.Empty<byte>(), "eae_prk", dh);
            sharedSecret = LabeledExpand(KemSuite, eaePrk, "shared_secret", OdohEncoding.Concat(enc, publicKey), 32);
            byte[] pskIdHash = LabeledExtract(HpkeSuite, Array.Empty<byte>(), "psk_id_hash", Array.Empty<byte>());
            byte[] infoHash = LabeledExtract(HpkeSuite, Array.Empty<byte>(), "info_hash", info);
            byte[] scheduleContext = OdohEncoding.Concat(new byte[] { 0 }, pskIdHash, infoHash);
            secret = LabeledExtract(HpkeSuite, sharedSecret, "secret", Array.Empty<byte>());
            return new HpkeContext(enc,
                LabeledExpand(HpkeSuite, secret, "key", scheduleContext, 16),
                LabeledExpand(HpkeSuite, secret, "base_nonce", scheduleContext, 12),
                LabeledExpand(HpkeSuite, secret, "exp", scheduleContext, 32));
        } finally {
            Array.Clear(ephemeral, 0, ephemeral.Length);
            Array.Clear(dh, 0, dh.Length);
            Clear(eaePrk);
            Clear(sharedSecret);
            Clear(secret);
        }
    }

    internal static byte[] Export(byte[] exporterSecret, byte[] context, int length) {
        return LabeledExpand(HpkeSuite, exporterSecret, "sec", context, length);
    }

    private static byte[] LabeledExtract(byte[] suite, byte[] salt, string label, byte[] ikm) {
        byte[] labeledIkm = OdohEncoding.Concat(Version, suite, Encoding.ASCII.GetBytes(label), ikm);
        try {
            return Extract(salt, labeledIkm);
        } finally {
            Array.Clear(labeledIkm, 0, labeledIkm.Length);
        }
    }

    private static byte[] LabeledExpand(byte[] suite, byte[] prk, string label, byte[] info, int length) {
        var encodedLength = new byte[2];
        OdohEncoding.WriteUInt16(encodedLength, 0, length);
        return Expand(prk, OdohEncoding.Concat(encodedLength, Version, suite, Encoding.ASCII.GetBytes(label), info), length);
    }

    internal static byte[] Extract(byte[] salt, byte[] ikm) {
        using (var hmac = new HMACSHA256(salt.Length == 0 ? new byte[32] : salt)) {
            return hmac.ComputeHash(ikm);
        }
    }

    internal static byte[] Expand(byte[] prk, byte[] info, int length) {
        if (length < 0 || length > 255 * 32) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        var result = new byte[length];
        byte[] previous = Array.Empty<byte>();
        try {
            using (var hmac = new HMACSHA256(prk)) {
                int written = 0;
                for (int block = 1; written < length; block++) {
                    byte[] input = OdohEncoding.Concat(previous, info, new[] { (byte)block });
                    Clear(previous);
                    try {
                        previous = hmac.ComputeHash(input);
                    } finally {
                        Array.Clear(input, 0, input.Length);
                    }
                    int count = Math.Min(previous.Length, length - written);
                    Buffer.BlockCopy(previous, 0, result, written, count);
                    written += count;
                }
            }
            return result;
        } finally {
            Clear(previous);
        }
    }

    internal static byte[] Aead(bool encrypt, byte[] key, byte[] nonce, byte[] aad, byte[] input) {
        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(encrypt, new AeadParameters(new KeyParameter(key), 128, nonce, aad));
        var result = new byte[cipher.GetOutputSize(input.Length)];
        try {
            int count = cipher.ProcessBytes(input, 0, input.Length, result, 0);
            count += cipher.DoFinal(result, count);
            if (count != result.Length) {
                throw new CryptographicException("Invalid ODoH AEAD output length.");
            }
            return result;
        } catch (InvalidCipherTextException error) {
            Array.Clear(result, 0, result.Length);
            throw new CryptographicException("ODoH response authentication failed.", error);
        }
    }

    internal static void Clear(byte[]? value) {
        if (value != null) {
            Array.Clear(value, 0, value.Length);
        }
    }
}
