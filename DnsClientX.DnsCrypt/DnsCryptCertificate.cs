using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace DnsClientX.DnsCrypt;

internal sealed class DnsCryptCertificate {
    internal ushort EncryptionSystem { get; private set; }
    internal byte[] ResolverPublicKey { get; private set; } = Array.Empty<byte>();
    internal byte[] ClientMagic { get; private set; } = Array.Empty<byte>();
    internal uint Serial { get; private set; }
    internal DateTimeOffset ValidFrom { get; private set; }
    internal DateTimeOffset ValidUntil { get; private set; }

    internal bool IsValidAt(DateTimeOffset now) => now >= ValidFrom && now <= ValidUntil;

    internal static DnsCryptCertificate? Parse(byte[] bytes, byte[] providerKey, DateTimeOffset now) {
        if (bytes.Length < 8 || bytes[0] != 'D' || bytes[1] != 'N' || bytes[2] != 'S' || bytes[3] != 'C') return null;
        ushort system = (ushort)((bytes[4] << 8) | bytes[5]);
        if ((system != 1 && system != 2) || bytes.Length < 124) return null;
        var signature = Slice(bytes, 8, 64);
        var signed = Slice(bytes, 72, bytes.Length - 72);
        if (!DnsCryptSodium.Verify(signature, signed, providerKey)) return null;
        uint start = ReadUInt32(bytes, 116), end = ReadUInt32(bytes, 120);
        if (start >= end) return null;
        var certificate = new DnsCryptCertificate {
            EncryptionSystem = system, ResolverPublicKey = Slice(bytes, 72, 32), ClientMagic = Slice(bytes, 104, 8),
            Serial = ReadUInt32(bytes, 112), ValidFrom = DateTimeOffset.FromUnixTimeSeconds(start), ValidUntil = DateTimeOffset.FromUnixTimeSeconds(end)
        };
        if (!certificate.IsValidAt(now) || certificate.ClientMagic.All(value => value == 255)
            || certificate.ClientMagic.Take(7).All(value => value == 0)
            || certificate.ClientMagic.SequenceEqual(new byte[] { 0x50, 0x51, 0x52, 0x65, 0x73, 0x75, 0x6d, 0x65 })) return null;
        var publicKey = new byte[32]; var secretKey = new byte[32]; byte[]? shared = null;
        try { DnsCryptSodium.KeyPair(publicKey, secretKey); shared = DnsCryptSodium.SharedKey(system, certificate.ResolverPublicKey, secretKey); }
        catch (CryptographicException) { return null; }
        finally { DnsCryptSodium.Zero(secretKey); if (shared != null) DnsCryptSodium.Zero(shared); }
        return certificate;
    }

    internal static DnsCryptCertificate Select(IEnumerable<byte[]> records, byte[] providerKey, DnsCryptEncryptionPolicy policy, DateTimeOffset now) {
        var certificates = records.Select(record => Parse(record, providerKey, now)).Where(certificate => certificate != null).Cast<DnsCryptCertificate>();
        if (policy == DnsCryptEncryptionPolicy.RequireXChaCha20) certificates = certificates.Where(certificate => certificate.EncryptionSystem == 2);
        else if (policy == DnsCryptEncryptionPolicy.RequireXSalsa20) certificates = certificates.Where(certificate => certificate.EncryptionSystem == 1);
        DnsCryptCertificate? selected = certificates.OrderByDescending(certificate => certificate.EncryptionSystem).ThenByDescending(certificate => certificate.Serial).FirstOrDefault();
        if (selected == null) throw new InvalidDataException("The resolver returned no valid signed DNSCrypt certificate allowed by the encryption policy.");
        return selected;
    }

    internal static byte[] Slice(byte[] bytes, int offset, int count) {
        var result = new byte[count]; Buffer.BlockCopy(bytes, offset, result, 0, count); return result;
    }
    private static uint ReadUInt32(byte[] bytes, int offset) => ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
}
