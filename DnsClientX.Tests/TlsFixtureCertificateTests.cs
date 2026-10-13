using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Verifies that software TLS fixtures leave no persistent Windows private-key file.</summary>
public class TlsFixtureCertificateTests {
    /// <summary>The named key needed by SChannel must exist only for the certificate's lifetime.</summary>
    [Fact]
    public void TemporarySoftwareKeyIsRemovedOnCertificateDisposal() {
        using RSA source = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", source, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string? keyFile = null;
        using (X509Certificate2 certificate = TestUtilities.CreateTlsCertificate(request)) {
            keyFile = ObserveTemporaryKeyFile(certificate);
        }
        Assert.False(File.Exists(keyFile), "Certificate disposal must remove the fixture's temporary key file.");
    }

    /// <summary>The issuer-signed discovery leaf must release the same temporary SChannel key when its fixture is disposed.</summary>
    [Fact]
    public void DiscoveryLeafTemporarySoftwareKeyIsRemovedWithFixture() {
        string? keyFile;
        using (var fixture = new DnsDiscoveryCertificateFixture(IPAddress.Loopback)) {
            keyFile = ObserveTemporaryKeyFile(fixture.Leaf);
        }
        Assert.False(File.Exists(keyFile), "Discovery fixture disposal must remove its leaf's temporary key file.");
    }

    private static string? ObserveTemporaryKeyFile(X509Certificate2 certificate) {
        Assert.True(certificate.HasPrivateKey);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
        // Inspect only the key generated for this fixture, never a certificate from a machine/user store.
        using RSA key = certificate.GetRSAPrivateKey()!;
        string cryptoRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Crypto");
        string? keyFile = null;
        if (key is RSACng cng) {
            keyFile = Path.Combine(cryptoRoot, "Keys", cng.Key.UniqueName!);
        } else if (key is RSACryptoServiceProvider csp) {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            keyFile = Path.Combine(cryptoRoot, "RSA", identity.User!.Value, csp.CspKeyContainerInfo.UniqueKeyContainerName);
        } else {
            Assert.Fail("The software fixture key must use a supported Windows provider.");
        }
        Assert.True(File.Exists(keyFile), "The fixture's temporary key file must be observable before disposal.");
        return keyFile;
    }
}
