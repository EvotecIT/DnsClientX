using System;
using System.IO;
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
            Assert.True(certificate.HasPrivateKey);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
            // Inspect only the key generated for this fixture, never a certificate from a machine/user store.
            using RSA key = certificate.GetRSAPrivateKey()!;
            string cryptoRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Crypto");
            if (key is RSACng cng) {
                keyFile = Path.Combine(cryptoRoot, "Keys", cng.Key.UniqueName!);
            } else if (key is RSACryptoServiceProvider csp) {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                keyFile = Path.Combine(cryptoRoot, "RSA", identity.User!.Value, csp.CspKeyContainerInfo.UniqueKeyContainerName);
            } else {
                Assert.Fail("The software fixture key must use a supported Windows provider.");
            }
            Assert.True(File.Exists(keyFile), "The fixture's temporary key file must be observable before disposal.");
        }
        Assert.False(File.Exists(keyFile), "Certificate disposal must remove the fixture's temporary key file.");
    }
}
