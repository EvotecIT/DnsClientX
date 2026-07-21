using System;

namespace DnsClientX {
    /// <summary>
    /// A shared secret and identity used to authenticate DNS UPDATE messages with TSIG.
    /// </summary>
    public sealed class TsigKey {
        private readonly byte[] _secret;

        /// <summary>
        /// Creates a TSIG key from raw secret bytes.
        /// </summary>
        public TsigKey(string name, byte[] secret, TsigAlgorithm algorithm = TsigAlgorithm.HmacSha256) {
            Name = DnsWireNameCodec.Normalize(name ?? throw new ArgumentNullException(nameof(name)));
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            if (secret.Length == 0) throw new ArgumentException("A TSIG secret cannot be empty.", nameof(secret));
            _secret = (byte[])secret.Clone();
            Algorithm = algorithm;
        }

        /// <summary>
        /// Gets the absolute TSIG key name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the selected HMAC algorithm.
        /// </summary>
        public TsigAlgorithm Algorithm { get; }

        /// <summary>
        /// Creates a TSIG key from a base64-encoded secret.
        /// </summary>
        public static TsigKey FromBase64(string name, string secret, TsigAlgorithm algorithm = TsigAlgorithm.HmacSha256) {
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            try {
                return new TsigKey(name, Convert.FromBase64String(secret), algorithm);
            } catch (FormatException ex) {
                throw new ArgumentException("The TSIG secret is not valid base64.", nameof(secret), ex);
            }
        }

        internal byte[] GetSecret() => (byte[])_secret.Clone();
    }
}
