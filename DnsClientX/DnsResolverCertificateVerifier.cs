using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DnsClientX;
/// <summary>Validates the original resolver IP binding required by RFC 9462 Verified Discovery.</summary>
public static class DnsResolverCertificateVerifier {
    /// <summary>Checks for an exact iPAddress SAN; DNS names and the certificate common name do not count.</summary>
    /// <param name = "certificate">The encrypted resolver's leaf certificate.</param>
    /// <param name = "resolverAddress">The original unencrypted resolver's address.</param>
    /// <returns>True only for an exact address match in a well-formed SAN extension.</returns>
    public static bool ContainsResolverAddress(X509Certificate2 certificate, IPAddress resolverAddress) {
        if (certificate == null) {
            throw new ArgumentNullException(nameof(certificate));
        }

        if (resolverAddress == null) {
            throw new ArgumentNullException(nameof(resolverAddress));
        }

        X509Extension[] extensions = certificate.Extensions.Cast<X509Extension>().Where(extension => extension.Oid?.Value == "2.5.29.17").ToArray();
        if (extensions.Length != 1) {
            return false;
        }

        try {
#if NET8_0_OR_GREATER
            var san = new X509SubjectAlternativeNameExtension(extensions[0].RawData);
            byte[] expected = resolverAddress.GetAddressBytes();
            // IPv6 scope IDs select a local interface; SAN encodes only the address octets.
            return san.EnumerateIPAddresses().Any(address => address.GetAddressBytes().SequenceEqual(expected));
#else
            // Legacy targets lack the BCL SAN extension decoder. Read only the bounded DER GeneralNames
            // sequence, with strict canonical lengths. This is not a general ASN.1 implementation.
            return LegacySanContainsAddress(extensions[0].RawData, resolverAddress.GetAddressBytes());
#endif
        }
        catch (CryptographicException) {
            return false;
        }
        catch (ArgumentException) {
            return false;
        }
    }

    /// <summary>Verifies the certificate chain and original resolver IP SAN without altering certificate stores.</summary>
    /// <param name = "certificate">The encrypted resolver's certificate.</param>
    /// <param name = "resolverAddress">The IP address that supplied the DDR answer.</param>
    /// <param name = "trustAnchors">Optional isolated application roots, supported on modern .NET.</param>
    /// <param name = "intermediates">Optional intermediate certificates.</param>
    /// <param name = "revocationMode">The explicit revocation-check policy; the default checks online.</param>
    /// <returns>True only when both the chain and exact IP SAN pass.</returns>
    public static bool Verify(X509Certificate2 certificate, IPAddress resolverAddress, IEnumerable<X509Certificate2>? trustAnchors = null, IEnumerable<X509Certificate2>? intermediates = null, X509RevocationMode revocationMode = X509RevocationMode.Online) {
        if (!ContainsResolverAddress(certificate, resolverAddress)) {
            return false;
        }

        return DnsTlsCertificateTrust.BuildChain(certificate, trustAnchors, intermediates, revocationMode);
    }
#if !NET8_0_OR_GREATER
    private static bool LegacySanContainsAddress(byte[] encoded, byte[] address) {
        if (encoded.Length > ushort.MaxValue || encoded.Length < 2 || encoded[0] != 0x30) {
            return false;
        }

        int cursor = 1;
        if (!ReadLength(encoded, ref cursor, encoded.Length, out int outerLength) || outerLength != encoded.Length - cursor) {
            return false;
        }

        bool found = false;
        while (cursor < encoded.Length) {
            byte tag = encoded[cursor++];
            // GeneralName is a context-specific alternative numbered 0-8, never a high-tag encoding.
            int choice = tag & 0x1f;
            if ((tag & 0xc0) != 0x80 || choice > 8) {
                return false;
            }

            if (!ReadLength(encoded, ref cursor, encoded.Length, out int length)) {
                return false;
            }

            if (choice == 7) {
                if (tag != 0x87 || length != 4 && length != 16) {
                    return false;
                }

                if (length == address.Length) {
                    bool equal = true;
                    for (int index = 0; index < length; index++) {
                        equal &= encoded[cursor + index] == address[index];
                    }

                    found |= equal;
                }
            }

            cursor += length;
        }

        return found;
    }

    private static bool ReadLength(byte[] bytes, ref int cursor, int end, out int length) {
        length = 0;
        if (cursor >= end) {
            return false;
        }

        int first = bytes[cursor++];
        if (first < 128) {
            length = first;
        }
        else {
            int count = first & 0x7f;
            if (count == 0 || count > 2 || cursor + count > end || bytes[cursor] == 0) {
                return false;
            }

            for (int index = 0; index < count; index++) {
                length = (length << 8) | bytes[cursor++];
            }

            if (length < 128 || count == 2 && length < 256) {
                return false;
            }
        }

        return length <= end - cursor;
    }
#endif
}
