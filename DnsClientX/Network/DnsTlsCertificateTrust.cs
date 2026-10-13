using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DnsClientX;
/// <summary>Owns the strict TLS trust policy shared by ordinary encrypted queries and DDR verification.</summary>
internal static class DnsTlsCertificateTrust {
    internal static bool Validate(X509Certificate? certificate, X509Chain? presentedChain, SslPolicyErrors errors, string authenticationName, IReadOnlyList<X509Certificate2>? roots, X509RevocationMode revocationMode) {
        if (certificate == null || (errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0) {
            return false;
        }

        if (roots == null || roots.Count == 0) {
            return errors == SslPolicyErrors.None && presentedChain != null && presentedChain.ChainStatus.Length == 0;
        }

#if NET8_0_OR_GREATER
        using var leaf = LoadCertificate(certificate.GetRawCertData());
        try {
            // An IPv6 zone identifier is local routing metadata and has no certificate SAN representation.
            string identity = NormalizeAuthenticationName(authenticationName);
            if (!leaf.MatchesHostname(identity, allowWildcards: true, allowCommonName: false)) {
                return false;
            }

            return BuildChain(leaf, roots, presentedChain?.ChainElements.Cast<X509ChainElement>().Skip(1).Select(element => element.Certificate), revocationMode);
        }
        catch (CryptographicException) {
            return false;
        }
#else
        throw new PlatformNotSupportedException("Isolated custom TLS trust anchors require modern .NET; persistent certificate stores are not modified.");
#endif
    }

    internal static string NormalizeAuthenticationName(string authenticationName) => IPAddress.TryParse(authenticationName, out IPAddress? address)
        ? new IPAddress(address.GetAddressBytes()).ToString() : authenticationName;

    internal static X509Certificate2 LoadCertificate(byte[] encoded) {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(encoded);
#else
        return new X509Certificate2(encoded);
#endif
    }

    internal static bool BuildChain(X509Certificate2 certificate, IEnumerable<X509Certificate2>? trustAnchors, IEnumerable<X509Certificate2>? intermediates, X509RevocationMode revocationMode) {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = revocationMode;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        if (intermediates != null) {
            foreach (X509Certificate2 intermediate in intermediates) {
                chain.ChainPolicy.ExtraStore.Add(intermediate);
            }
        }

        X509Certificate2[] roots = (trustAnchors ?? Array.Empty<X509Certificate2>()).ToArray();
        if (roots.Length > 0) {
#if NET8_0_OR_GREATER
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            foreach (X509Certificate2 root in roots) {
                chain.ChainPolicy.CustomTrustStore.Add(root);
            }
#else
            throw new PlatformNotSupportedException("Isolated custom TLS trust anchors require modern .NET; persistent certificate stores are not modified.");
#endif
        }

        try {
            return chain.Build(certificate);
        }
        catch (CryptographicException) {
            return false;
        }
    }
}
