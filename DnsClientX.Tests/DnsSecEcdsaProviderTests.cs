using System;
using System.IO;
using DnsClientX.DnsSec.EdDsa;

namespace DnsClientX.Tests;

/// <summary>Verifies portable ECDSA using the public RFC 6605 DNSSEC examples.</summary>
public class DnsSecEcdsaProviderTests {
    /// <summary>Both RFC fixed-width signatures validate on every target and reject tampering.</summary>
    [Theory]
    [InlineData(DnsKeyAlgorithm.ECDSAP256SHA256, 55648, "20100909100439", "20100812100439",
        "GojIhhXUN/u4v54ZQqGSnyhWJwaubCvTmeexv7bR6edbkrSqQpF64cYbcB7wNcP+e+MAnLr+Wi9xMWyQLc8NAA==",
        "qx6wLYqmh+l9oCKTN6qIc+bw6ya+KJ8oMz0YP107epXAyGmt+3SNruPFKG7tZoLBLlUzGGus7ZwmwWep666VCw==")]
    [InlineData(DnsKeyAlgorithm.ECDSAP384SHA384, 10771, "20100909102025", "20100812102025",
        "xKYaNhWdGOfJ+nPrL8/arkwf2EY3MDJ+SErKivBVSum1w/egsXvSADtNJhyem5RCOpgQ6K8X1DRSEkrbYQ+OB+v8/uX45NBwY8rp65F6Glur8I/mlVNgF6W/qTI37m40",
        "/L5hDKIvGDyI1fcARX3z65qrmPsVz73QD1Mr5CEqOiLP95hxQouuroGCeZOvzFaxsT8Glr74hbavRKayJNuydCuzWTSSPdz7wnqXL5bdcJzusdnI0RSMROxxwGipWcJm")]
    public void PublishedRfc6605ExamplesValidate(DnsKeyAlgorithm algorithm, ushort tag, string expires,
        string inception, string publicKey, string signature) {
        byte[] keyBytes = Convert.FromBase64String(publicKey);
        var key = new DnsSecKey("example.net", 257, 3, (byte)algorithm, keyBytes);
        byte[] data = SignedData(algorithm, tag, expires, inception);
        byte[] signatureBytes = Convert.FromBase64String(signature);
        var provider = new EdDsaDnsSecSignatureVerifier();
        Assert.Equal(tag, key.KeyTag);
        Assert.True(provider.Verify(algorithm, keyBytes, data, signatureBytes));
        Assert.True(DnsSecCrypto.Verify(key, data, signatureBytes, provider));
        data[data.Length - 1] ^= 1;
        Assert.False(DnsSecCrypto.Verify(key, data, signatureBytes, provider));
        Assert.False(provider.Verify(algorithm, new byte[keyBytes.Length], data, signatureBytes));
        Assert.False(provider.Verify(algorithm, keyBytes, data, new byte[signatureBytes.Length - 1]));
        Assert.False(provider.Verify(algorithm, keyBytes, data, new byte[signatureBytes.Length]));
    }

    /// <summary>Capabilities expose target differences and the same optional algorithms on all targets.</summary>
    [Fact]
    public void ConfigurationCapabilitiesIncludeConfiguredProvider() {
        var configuration = new Configuration(DnsEndpoint.Cloudflare);
        Assert.Contains(DnsKeyAlgorithm.RSASHA256, configuration.SupportedDnsSecAlgorithms);
        Assert.DoesNotContain(DnsKeyAlgorithm.ED25519, configuration.SupportedDnsSecAlgorithms);
#if NET5_0_OR_GREATER
        Assert.Contains(DnsKeyAlgorithm.ECDSAP256SHA256, configuration.SupportedDnsSecAlgorithms);
#else
        Assert.DoesNotContain(DnsKeyAlgorithm.ECDSAP256SHA256, configuration.SupportedDnsSecAlgorithms);
#endif
        configuration.UseEdDsaDnsSec();
        Assert.Equal(new[] { DnsKeyAlgorithm.RSASHA1, DnsKeyAlgorithm.RSASHA1NSEC3SHA1,
            DnsKeyAlgorithm.RSASHA256, DnsKeyAlgorithm.RSASHA512, DnsKeyAlgorithm.ECDSAP256SHA256,
            DnsKeyAlgorithm.ECDSAP384SHA384, DnsKeyAlgorithm.ED25519, DnsKeyAlgorithm.ED448 },
            configuration.SupportedDnsSecAlgorithms);
    }

    /// <summary>Explicit local loading finds the actual optional provider; unrelated assemblies fail.</summary>
    [Fact]
    public void LoaderUsesOptionalAssemblyAndRejectsNonProvider() {
        var provider = DnsSecSignatureVerifierLoader.Load(typeof(EdDsaDnsSecSignatureVerifier).Assembly.Location);
        Assert.IsType<EdDsaDnsSecSignatureVerifier>(provider);
        Assert.True(provider.SupportsAlgorithm(DnsKeyAlgorithm.ECDSAP256SHA256));
        Assert.Throws<InvalidOperationException>(() => DnsSecSignatureVerifierLoader.Load(typeof(ClientX).Assembly.Location));
    }

    /// <summary>Query, probe and benchmark client creation preserves the explicitly configured verifier.</summary>
    [Fact]
    public void SharedClientCreationPreservesProvider() {
        var provider = new EdDsaDnsSecSignatureVerifier();
        var target = new ResolverExecutionTarget { BuiltInEndpoint = DnsEndpoint.Cloudflare, DisplayName = "Cloudflare" };
        using var query = ResolverExecutionClientFactory.CreateClient(target,
            new ResolverExecutionClientOptions { DnsSecSignatureVerifier = provider });
        using var workflow = ResolverQueryExecutor.CreateClient(target,
            new ResolverQueryRunOptions { DnsSecSignatureVerifier = provider });
        Assert.Same(provider, query.EndpointConfiguration.DnsSecSignatureVerifier);
        Assert.Same(provider, workflow.EndpointConfiguration.DnsSecSignatureVerifier);
        Assert.Same(provider, new MultiResolverOptions { DnsSecSignatureVerifier = provider }.Clone().DnsSecSignatureVerifier);
    }

    /// <summary>A configured provider's rejection is definitive even when core supports the algorithm.</summary>
    [Fact]
    public void ConfiguredProviderRejectionDoesNotFallBack() {
        var provider = new RejectingVerifier();
        var key = new DnsSecKey("example.net", 257, 3, (byte)DnsKeyAlgorithm.RSASHA256, Array.Empty<byte>());
        Assert.False(DnsSecCrypto.Verify(key, Array.Empty<byte>(), Array.Empty<byte>(), provider));
        Assert.True(provider.Called);
    }

    private sealed class RejectingVerifier : IDnsSecSignatureVerifier {
        public string Name => "Test rejection";
        public bool Called { get; private set; }
        public bool SupportsAlgorithm(DnsKeyAlgorithm algorithm) => algorithm == DnsKeyAlgorithm.RSASHA256;
        public bool Verify(DnsKeyAlgorithm algorithm, byte[] publicKey, byte[] data, byte[] signature) { Called = true; return false; }
    }

    private static byte[] SignedData(DnsKeyAlgorithm algorithm, ushort tag, string expiration, string inception) {
        using var output = new MemoryStream();
        Write16(output, 1);
        output.WriteByte((byte)algorithm);
        output.WriteByte(3);
        Write32(output, 3600);
        Write32(output, Time(expiration));
        Write32(output, Time(inception));
        Write16(output, tag);
        WriteName(output, "example.net");
        WriteName(output, "www.example.net");
        Write16(output, 1);
        Write16(output, 1);
        Write32(output, 3600);
        Write16(output, 4);
        output.Write(new byte[] { 192, 0, 2, 1 }, 0, 4);
        return output.ToArray();
    }

    private static uint Time(string value) => checked((uint)new DateTimeOffset(DateTime.ParseExact(value,
        "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal)).ToUnixTimeSeconds());
    private static void WriteName(Stream output, string value) { byte[] bytes = DnsWireNameCodec.ToCanonicalWire(value); output.Write(bytes, 0, bytes.Length); }
    private static void Write16(Stream output, ushort value) { output.WriteByte((byte)(value >> 8)); output.WriteByte((byte)value); }
    private static void Write32(Stream output, uint value) { output.WriteByte((byte)(value >> 24)); output.WriteByte((byte)(value >> 16)); output.WriteByte((byte)(value >> 8)); output.WriteByte((byte)value); }
}
