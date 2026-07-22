using System;
using System.IO;
using DnsClientX.DnsSec.EdDsa;

namespace DnsClientX.Tests;

/// <summary>Protects the optional RFC 8080 verifier against the published DNSSEC examples.</summary>
public sealed class DnsSecEdDsaTests {
    /// <summary>Builds a live chain through zones signed with each RFC 8080 algorithm.</summary>
    [RealDnsFact]
    public System.Threading.Tasks.Task ValidateAsync_AcceptsLiveEd25519Zone() =>
        ValidateLiveZoneAsync("ed25519.nl");

    /// <summary>Builds a live chain through a zone signed with Ed448.</summary>
    [RealDnsFact]
    public System.Threading.Tasks.Task ValidateAsync_AcceptsLiveEd448Zone() =>
        ValidateLiveZoneAsync("ed448.no");

    private static async System.Threading.Tasks.Task ValidateLiveZoneAsync(string name) {
        using var client = new ClientX(DnsEndpoint.RootServer);
        using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
        client.EndpointConfiguration.UseEdDsaDnsSec();

        DnsResponse response = await client.Resolve(name, DnsRecordType.SOA,
            requestDnsSec: true, validateDnsSec: true, retryOnTransient: false,
            cancellationToken: timeout.Token);

        Assert.True(response.DnsSecValidationStatus == DnsSecValidationStatus.Secure,
            $"{name}: {response.DnsSecValidationStatus}: {response.DnsSecValidationMessage}");
        Assert.NotEmpty(response.Answers);
    }

    /// <summary>Verifies the RFC 8080 Ed25519 and Ed448 MX RRset examples end to end.</summary>
    [Theory]
    [InlineData(DnsKeyAlgorithm.ED25519, 3613,
        "l02Woi0iS8Aa25FQkUd9RMzZHJpBoRQwAQEX1SxZJA4=",
        "Edk+IB9KNNWg0HAjm7FazXyrd5m3Rk8zNZbvNpAcM+eysqcUOMIjWoevFkjH5GaMWeG96GUVZu6ECKOQmemHDg==")]
    [InlineData(DnsKeyAlgorithm.ED448, 9713,
        "3kgROaDjrh0H2iuixWBrc8g2EpBBLCdGzHmn+G2MpTPhpj/OiBVHHSfPodx1FYYUcJKm1MDpJtIA",
        "Nmc0rgGKpr3GKYXcB1JmqqS4NYwhmechvJTqVzt3jR+Qy/lSLFoIk1L+9e39GPL+5tVzDPN3f9kAwiu8KCuPPjtl227ayaCZtRKZuJax7n9NuYlZJIusX0SOIOKBGzG+yWYtz1/jjbzl5GGkWvREUCUA")]
    public void Verify_AcceptsPublishedRfc8080Example(DnsKeyAlgorithm algorithm, ushort keyTag,
        string publicKeyBase64, string signatureBase64) {
        byte[] publicKey = Convert.FromBase64String(publicKeyBase64);
        byte[] signature = Convert.FromBase64String(signatureBase64);
        byte[] signedData = BuildExampleSignedData(algorithm, keyTag);
        var key = new DnsSecKey("example.com", 257, 3, (byte)algorithm, publicKey);
        var verifier = new EdDsaDnsSecSignatureVerifier();

        Assert.Equal(keyTag, key.KeyTag);
        Assert.True(verifier.SupportsAlgorithm(algorithm));
        Assert.True(DnsSecCrypto.IsSupportedAlgorithm((byte)algorithm, verifier));
        Assert.True(DnsSecCrypto.Verify(key, signedData, signature, verifier));

        signedData[signedData.Length - 1] ^= 1;
        Assert.False(DnsSecCrypto.Verify(key, signedData, signature, verifier));
    }

    /// <summary>Rejects malformed key and signature lengths without throwing.</summary>
    [Fact]
    public void Verify_RejectsMalformedLengths() {
        var verifier = new EdDsaDnsSecSignatureVerifier();

        Assert.False(verifier.Verify(DnsKeyAlgorithm.ED25519, new byte[31], new byte[1], new byte[64]));
        Assert.False(verifier.Verify(DnsKeyAlgorithm.ED448, new byte[57], new byte[1], new byte[113]));
        Assert.False(verifier.Verify(DnsKeyAlgorithm.RSASHA256, new byte[32], new byte[1], new byte[64]));
    }

    private static byte[] BuildExampleSignedData(DnsKeyAlgorithm algorithm, ushort keyTag) {
        byte[] signer = DnsWireNameCodec.ToCanonicalWire("example.com");
        byte[] exchange = DnsWireNameCodec.ToCanonicalWire("mail.example.com");
        using var output = new MemoryStream();

        WriteUInt16(output, (ushort)DnsRecordType.MX);
        output.WriteByte((byte)algorithm);
        output.WriteByte(3);
        WriteUInt32(output, 3600);
        WriteUInt32(output, 1440021600);
        WriteUInt32(output, 1438207200);
        WriteUInt16(output, keyTag);
        output.Write(signer, 0, signer.Length);

        output.Write(signer, 0, signer.Length);
        WriteUInt16(output, (ushort)DnsRecordType.MX);
        WriteUInt16(output, 1);
        WriteUInt32(output, 3600);
        WriteUInt16(output, checked((ushort)(2 + exchange.Length)));
        WriteUInt16(output, 10);
        output.Write(exchange, 0, exchange.Length);
        return output.ToArray();
    }

    private static void WriteUInt16(Stream stream, ushort value) {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteUInt32(Stream stream, uint value) {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
}
