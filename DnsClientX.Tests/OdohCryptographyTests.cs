using System;
using System.IO;
using System.Security.Cryptography;
using DnsClientX.Odoh;

namespace DnsClientX.Tests;

/// <summary>Independent RFC 9180 and Cloudflare producer contracts for the ODoH schedule and framing.</summary>
public class OdohCryptographyTests {
    /// <summary>RFC 9180 A.1.1 / CFRG published base-mode vector, including domain-separated exporter outputs.</summary>
    [Fact]
    public void RequiredSuiteMatchesPublishedHpkeCiphertextAndExports() {
        // https://github.com/cfrg/draft-irtf-cfrg-hpke/blob/master/test-vectors.json
        using HpkeContext context = HpkeX25519.CreateSender(
            OdohTestVectors.Hex("3948cfe0ad1ddb695d780e59077195da6c56506b027329794ab02bca80815c4d"),
            OdohTestVectors.Hex("4f6465206f6e2061204772656369616e2055726e"),
            OdohTestVectors.Hex("52c4a758a802cd8b936eceea314432798d5baf2d7e9235dc084ab1b9cfa2f736"));
        Assert.Equal(OdohTestVectors.Hex("37fda3567bdbd628e88668c3c8d7e97d1d1253b6d4ea6d44c150f741f1bf4431"), context.Enc);
        Assert.Equal(OdohTestVectors.Hex("f938558b5d72f1a23810b4be2ab4f84331acc02fc97babc53a52ae8218a355a96d8770ac83d07bea87e13c512a"),
            context.Seal(OdohTestVectors.Hex("436f756e742d30"), OdohTestVectors.Hex("4265617574792069732074727574682c20747275746820626561757479")));
        Assert.Equal(OdohTestVectors.Hex("3853fe2b4035195a573ffc53856e77058e15d9ea064de3e59f4961d0095250ee"), context.Export(Array.Empty<byte>(), 32));
        Assert.Equal(OdohTestVectors.Hex("2e8f0b54673c7029649d4eb9d5e33bf1872cf76d623ff164ac185da9e88c21a5"), context.Export(new byte[] { 0 }, 32));
        Assert.Equal(OdohTestVectors.Hex("e9e43065102c3836401bed8c3c3c75ae46be1639869391d62c61f1ec7af54931"), context.Export(OdohTestVectors.Hex("54657374436f6e74657874"), 32));
        Assert.Throws<InvalidOperationException>(() => context.Seal(Array.Empty<byte>(), new byte[] { 1 }));
    }

    /// <summary>The entire ODoH key ID, encrypted query and decrypted response agree with a separate producer.</summary>
    [Fact]
    public void CompleteExchangeMatchesCloudflareGoProducer() {
        OdohTargetConfiguration configuration = FixtureConfiguration();
        Assert.Equal(OdohTestVectors.Hex(OdohTestVectors.KeyId), configuration.KeyId);
        using OdohQueryContext context = FixtureContext();
        Assert.Equal(OdohTestVectors.Hex(OdohTestVectors.EncryptedQuery), context.Message);
        Assert.Equal(OdohTestVectors.Hex(OdohTestVectors.Response), context.OpenResponse(OdohTestVectors.Hex(OdohTestVectors.EncryptedResponse)));
    }

    /// <summary>Nonce and ciphertext modification cannot yield plaintext; authenticated nonzero padding is rejected too.</summary>
    [Fact]
    public void TamperingWrongQueryAndAuthenticatedBadPaddingAreRejected() {
        using OdohQueryContext context = FixtureContext();
        byte[] ciphertextTamper = OdohTestVectors.Hex(OdohTestVectors.EncryptedResponse);
        ciphertextTamper[ciphertextTamper.Length - 1] ^= 1;
        Assert.Throws<CryptographicException>(() => context.OpenResponse(ciphertextTamper));
        byte[] nonceTamper = OdohTestVectors.Hex(OdohTestVectors.EncryptedResponse);
        nonceTamper[3] ^= 1;
        Assert.Throws<CryptographicException>(() => context.OpenResponse(nonceTamper));
        Assert.Throws<InvalidDataException>(() => context.OpenResponse(OdohTestVectors.Hex(OdohTestVectors.BadPaddingResponse)));
        byte[] otherQuery = OdohTestVectors.Hex(OdohTestVectors.Query);
        otherQuery[0] ^= 1;
        using OdohQueryContext other = OdohQueryContext.Create(FixtureConfiguration(), otherQuery, 44, OdohTestVectors.Hex(OdohTestVectors.EphemeralPrivateKey));
        Assert.Throws<CryptographicException>(() => other.OpenResponse(OdohTestVectors.Hex(OdohTestVectors.EncryptedResponse)));
    }

    /// <summary>Every possible truncation of a real response and trailing framing bytes are rejected.</summary>
    [Fact]
    public void TruncatedAndTrailingResponsesAreRejected() {
        using OdohQueryContext context = FixtureContext();
        byte[] response = OdohTestVectors.Hex(OdohTestVectors.EncryptedResponse);
        for (int length = 0; length < response.Length; length++) {
            var truncated = new byte[length];
            Array.Copy(response, truncated, length);
            Assert.Throws<InvalidDataException>(() => context.OpenResponse(truncated));
        }
        var trailing = new byte[response.Length + 1];
        Array.Copy(response, trailing, response.Length);
        Assert.Throws<InvalidDataException>(() => context.OpenResponse(trailing));
        response[0] = 1;
        Assert.Throws<InvalidDataException>(() => context.OpenResponse(response));
    }

    /// <summary>Independent sender contexts do not reuse encapsulation or response secrets for equal DNS queries.</summary>
    [Fact]
    public void IdenticalQueriesHaveIndependentEphemeralContexts() {
        using OdohQueryContext first = OdohQueryContext.Create(FixtureConfiguration(), OdohTestVectors.Hex(OdohTestVectors.Query), 128);
        using OdohQueryContext second = OdohQueryContext.Create(FixtureConfiguration(), OdohTestVectors.Hex(OdohTestVectors.Query), 128);
        Assert.NotEqual(first.Message, second.Message);
        Assert.Throws<CryptographicException>(() => first.OpenResponse(OdohTestVectors.Hex(OdohTestVectors.EncryptedResponse)));
    }

    internal static OdohTargetConfiguration FixtureConfiguration() {
        return OdohTargetConfiguration.Parse(OdohTestVectors.Hex(OdohTestVectors.Config), DateTimeOffset.UtcNow.AddHours(1));
    }

    private static OdohQueryContext FixtureContext() {
        return OdohQueryContext.Create(FixtureConfiguration(), OdohTestVectors.Hex(OdohTestVectors.Query), 44, OdohTestVectors.Hex(OdohTestVectors.EphemeralPrivateKey));
    }
}
