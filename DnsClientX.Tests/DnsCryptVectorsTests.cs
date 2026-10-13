using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DnsClientX.DnsCrypt;

namespace DnsClientX.Tests;

/// <summary>Fixed official DNSCrypt Appendix B vectors, independent of provider-generated ciphertext.</summary>
public class DnsCryptVectorsTests {
    // Source: DNSCrypt work-in-progress draft, 23 September 2026, Appendix B.
    // https://dnscrypt.github.io/dnscrypt-protocol/draft-denis-dprive-dnscrypt.html#name-dnscrypt-test-vectors
    internal static readonly byte[] ProviderKey = Hex("03a107bff3ce10be1d70dd18e74bc09967e4d6309ba50d5f1ddc8664125531b8");
    internal static readonly byte[] ResolverKey = Hex("358072d6365880d1aeea329adf9121383851ed21a28e3b75e965d0d2cd166254");
    internal static readonly byte[] ClientKey = Hex("79a631eede1bf9c98f12032cdeadd0e7a079398fc786b88cc846ec89af85a51a");
    internal static readonly byte[] Query = Hex("12340100000100000000000003777777076578616d706c6503636f6d0000010001");
    internal static readonly byte[] Response = Hex("12348180000100010000000003777777076578616d706c6503636f6d0000010001c00c0001000100000e1000045db8d822");
    internal static readonly byte[] Certificate = Hex("444e5343000200003a570ea17f47b80217977fbb455840bfd50ab32f5fbf2aabc173a6a49b7a49ca55362a6c5dec47657cf515e9f99382a316dfecd964b94d1c4659cac45961400c358072d6365880d1aeea329adf9121383851ed21a28e3b75e965d0d2cd166254b1b2b3b4b5b6b7b8000000016800000068015180");
    private static readonly byte[] EncryptedQuery = Hex("2dae527c26386d5cd4e61152db6dd1812ff6aaf7644fc122afc70b1b580b18f10fbc26577abc759152cde31cd0afc5c5f452f8654815469723300819bed5a12015c044b94d63ec1f79e48a23968e437feb8bb8720cf4e60a0499746190c8b3eb83aeb0d858df77794270b861f86644502be0d22d6f0b2b132e9ca68538300c8d68b8e3c48190cbbf96d602f38dfc3b4d642016ceeaf4bc2c2ded9483b9f9d4eed703a0bebc252add8822d4b9152e30670bcde9ea75a0e3e67ea576e9b1262bb2b25b4f9432311b75a2238b34bf4f868da182b85dccb1762a703bba31d04d77b4c57ec9039663959793677588b3a74ae409b0f16374dd64cbd6d47d801725b014ce9ddaf6f1aa30688c8efcbfde1d5d1d");
    internal static readonly byte[] ResponsePacket = Hex("7236666e76576a38a0a1a2a3a4a5a6a7a8a9aaabc0c1c2c3c4c5c6c7c8c9cacbf2670995c6d37c2f8d2016029dd5970b893de83c02815ece9b48d9fd0b0dca8741674142fbd8e12c1120b111f366326aa71c89823a2931ac5c860dad49685ed6cc22cc13e829d2e51d1c00ea64d1d39d");
    private static readonly DateTimeOffset VectorTime = DateTimeOffset.FromUnixTimeSeconds(0x68000001);

    /// <summary>Checks exact independent cryptographic and packet bytes against the native construction.</summary>
    [Fact]
    public void OfficialXChaChaSharedKeyAndFullWireVectorsMatchNativeBackend() {
        var certificate = DnsCryptCertificate.Parse(Certificate, ProviderKey, VectorTime)!;
        byte[] secret = Enumerable.Range(0x40, 32).Select(value => (byte)value).ToArray();
        byte[] shared = DnsCryptSodium.SharedKey(2, ResolverKey, secret);
        try {
            Assert.Equal(Hex("335d32f2d65e6623cbbd05b6539c9575fee16cb5405fe839ab4bd291fdf13262"), shared);
            byte[] nonce = Enumerable.Range(0xa0, 12).Select(value => (byte)value).ToArray();
            byte[] packet = DnsCryptWire.EncryptQuery(certificate, Query, ClientKey, shared, nonce, 256);
            Assert.Equal(EncryptedQuery, packet.Skip(52));
            Assert.Equal(Hex("b1b2b3b4b5b6b7b8").Concat(ClientKey).Concat(nonce).Concat(EncryptedQuery), packet);
            Assert.Equal(Response, DnsCryptWire.DecryptResponse(certificate, Query, ResponsePacket, shared, nonce));
        } finally { DnsCryptSodium.Zero(secret); DnsCryptSodium.Zero(shared); }
    }

    /// <summary>Requires signed certificate content and the inclusive published validity window.</summary>
    [Fact]
    public void OfficialCertificateSignatureValidityAndSelectionRejectModifiedEvidence() {
        Assert.NotNull(DnsCryptCertificate.Parse(Certificate, ProviderKey, VectorTime));
        Assert.NotNull(DnsCryptCertificate.Parse(Certificate, ProviderKey, DateTimeOffset.FromUnixTimeSeconds(0x68000000)));
        Assert.NotNull(DnsCryptCertificate.Parse(Certificate, ProviderKey, DateTimeOffset.FromUnixTimeSeconds(0x68015180)));
        Assert.Null(DnsCryptCertificate.Parse(Certificate, ProviderKey, VectorTime.AddDays(2)));
        byte[] tampered = (byte[])Certificate.Clone(); tampered[72] ^= 1;
        Assert.Null(DnsCryptCertificate.Parse(tampered, ProviderKey, VectorTime));
        byte[] extension = Certificate.Concat(new byte[] { 1 }).ToArray();
        Assert.Null(DnsCryptCertificate.Parse(extension, ProviderKey, VectorTime));
        Assert.Null(DnsCryptCertificate.Parse(Certificate.Take(123).ToArray(), ProviderKey, VectorTime));
        Assert.Null(DnsCryptCertificate.Parse(Certificate, new byte[32], VectorTime));
        Assert.Equal((uint)1, DnsCryptCertificate.Select(new[] { tampered, Certificate }, ProviderKey, DnsCryptEncryptionPolicy.RequireXChaCha20, VectorTime).Serial);
        Assert.Throws<InvalidDataException>(() => DnsCryptCertificate.Select(new[] { Certificate }, ProviderKey, DnsCryptEncryptionPolicy.RequireXSalsa20, VectorTime));
    }

    /// <summary>Rejects unauthenticated ciphertext, mismatched responses, and low-order resolver keys.</summary>
    [Fact]
    public void NativeAuthenticationRejectsTagCiphertextWrongNonceAndLowOrderKeys() {
        var certificate = DnsCryptCertificate.Parse(Certificate, ProviderKey, VectorTime)!;
        byte[] secret = Enumerable.Range(0x40, 32).Select(value => (byte)value).ToArray();
        byte[] shared = DnsCryptSodium.SharedKey(2, ResolverKey, secret);
        byte[] nonce = Enumerable.Range(0xa0, 12).Select(value => (byte)value).ToArray();
        try {
            foreach (int offset in new[] { 32, 48, 111 }) {
                byte[] tampered = (byte[])ResponsePacket.Clone(); tampered[offset] ^= 1;
                Assert.Throws<CryptographicException>(() => DnsCryptWire.DecryptResponse(certificate, Query, tampered, shared, nonce));
            }
            nonce[0] ^= 1;
            Assert.Throws<InvalidDataException>(() => DnsCryptWire.DecryptResponse(certificate, Query, ResponsePacket, shared, nonce));
            nonce[0] ^= 1;
            Assert.Throws<InvalidDataException>(() => DnsCryptWire.DecryptResponse(certificate, Query, ResponsePacket.Take(60).ToArray(), shared, nonce));
            Assert.Throws<CryptographicException>(() => DnsCryptWire.DecryptResponse(certificate, Query, ResponsePacket.Take(111).ToArray(), shared, nonce));
            byte[] differentQuery = (byte[])Query.Clone(); differentQuery[0] ^= 1;
            Assert.Throws<InvalidDataException>(() => DnsCryptWire.DecryptResponse(certificate, differentQuery, ResponsePacket, shared, nonce));
            Assert.Throws<CryptographicException>(() => DnsCryptSodium.SharedKey(2, new byte[32], secret));
            Assert.Throws<CryptographicException>(() => DnsCryptSodium.SharedKey(1, new byte[32], secret));
        } finally { DnsCryptSodium.Zero(secret); DnsCryptSodium.Zero(shared); }
    }

    /// <summary>Requires the ISO padding delimiter and a distinct resolver nonce.</summary>
    [Fact]
    public void ResponsePaddingAndZeroResolverNonceAreRejected() {
        Assert.Throws<InvalidDataException>(() => DnsCryptWire.Unpad(new byte[64]));
        Assert.Throws<InvalidDataException>(() => DnsCryptWire.Unpad(Query.Concat(new byte[] { 0x81, 0 }).ToArray()));
        var certificate = DnsCryptCertificate.Parse(Certificate, ProviderKey, VectorTime)!;
        byte[] zero = (byte[])ResponsePacket.Clone(); Array.Clear(zero, 20, 12);
        Assert.Throws<InvalidDataException>(() => DnsCryptWire.DecryptResponse(certificate, Query, zero, new byte[32], Enumerable.Range(0xa0, 12).Select(value => (byte)value).ToArray()));
    }

    /// <summary>Concatenates binary TXT strings within a certificate record.</summary>
    [Fact]
    public void CertificateDiscoveryReadsOfficialBinaryTxtAndMultipleCharacterStrings() {
        byte[] query = Hex("abcd0100000100000000000001320d646e7363727970742d63657274076578616d706c6503636f6d0000100001");
        byte[] response = Hex("abcd8180000100010000000001320d646e7363727970742d63657274076578616d706c6503636f6d0000100001c00c0010000100015180007d7c").Concat(Certificate).ToArray();
        Assert.Equal(Certificate, Assert.Single(DnsCryptWire.CertificateRecords(query, response)));
        // Split one certificate into two TXT strings; records themselves remain separate.
        byte[] split = response.Take(response.Length - 125).Concat(new byte[] { 62 }).Concat(Certificate.Take(62)).Concat(new byte[] { 62 }).Concat(Certificate.Skip(62)).ToArray();
        split[55] = 0; split[56] = 126;
        Assert.Equal(Certificate, Assert.Single(DnsCryptWire.CertificateRecords(query, split)));
    }

    internal static byte[] Hex(string value) {
        var bytes = new byte[value.Length / 2];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
        return bytes;
    }
}
