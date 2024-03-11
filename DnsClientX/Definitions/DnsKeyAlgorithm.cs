using System;

namespace DnsClientX {

    /// <summary>
    /// An enumeration of the DNSKEY algorithms.
    /// </summary>
    public enum DnsKeyAlgorithm {
        /// <summary>
        /// RSA/MD5
        /// </summary>
        RSAMD5 = 1,

        /// <summary>
        /// Diffie-Hellman
        /// </summary>
        DH = 2,

        /// <summary>
        /// DSA/SHA-1
        /// </summary>
        DSA = 3,

        /// <summary>
        /// Elliptic Curve
        /// </summary>
        ECC = 4,

        /// <summary>
        /// RSA/SHA-1
        /// </summary>
        RSASHA1 = 5,

        /// <summary>
        /// DSA-NSEC3-SHA1
        /// </summary>
        DSANSEC3SHA1 = 6,

        /// <summary>
        /// RSA/SHA-1 NSEC3
        /// </summary>
        RSASHA1NSEC3SHA1 = 7,

        /// <summary>
        /// RSA/SHA-256
        /// </summary>
        RSASHA256 = 8,

        /// <summary>
        /// RSA/SHA-512
        /// </summary>
        RSASHA512 = 10,

        /// <summary>
        /// ECC/GOST
        /// </summary>
        ECCGOST = 12,

        /// <summary>
        /// ECDSA/P-256 with SHA-256
        /// </summary>
        ECDSAP256SHA256 = 13,

        /// <summary>
        /// ECDSA/P-384 with SHA-384
        /// </summary>
        ECDSAP384SHA384 = 14,

        /// <summary>
        /// ED25519
        /// </summary>
        ED25519 = 15,

        /// <summary>
        /// ED448
        /// </summary>
        ED448 = 16,

        /// <summary>
        /// Indirect
        /// </summary>
        INDIRECT = 252,

        /// <summary>
        /// Private DNS
        /// </summary>
        PRIVATEDNS = 253,

        /// <summary>
        /// Private OID
        /// </summary>
        PRIVATEOID = 254
    }

    public static class DnsKeyAlgorithmExtensions {
        public static DnsKeyAlgorithm FromValue(int value) {
            if (Enum.IsDefined(typeof(DnsKeyAlgorithm), value)) {
                return (DnsKeyAlgorithm)value;
            } else {
                throw new ArgumentException($"Invalid value for DnsKeyAlgorithm: {value}");
            }
        }
    }
}
