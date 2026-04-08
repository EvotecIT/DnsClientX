using System.Collections.Generic;

namespace DnsClientX {
    /// <summary>
    /// Represents EDNS options used when sending DNS queries.
    /// </summary>
    public class EdnsOptions {
        /// <summary>
        /// Gets or sets a value indicating whether EDNS should be enabled.
        /// </summary>
        public bool EnableEdns { get; set; } = true;

        /// <summary>
        /// Gets or sets the UDP buffer size used for EDNS queries.
        /// </summary>
        public int UdpBufferSize { get; set; } = 4096;

        /// <summary>
        /// Gets or sets the EDNS Client Subnet (ECS) in CIDR notation.
        /// </summary>
        public EdnsClientSubnetOption? Subnet { get; set; }

        /// <summary>
        /// Gets or sets the EDNS padding length in bytes.
        /// </summary>
        public int PaddingLength { get; set; }

        /// <summary>
        /// Gets or sets the EDNS cookie payload.
        /// </summary>
        public byte[]? Cookie { get; set; }

        /// <summary>
        /// Gets the additional EDNS options to include in the OPT record.
        /// </summary>
        public System.Collections.Generic.List<EdnsOption> Options { get; } = [];

        /// <summary>
        /// Returns the effective EDNS options after applying convenience properties.
        /// </summary>
        internal IEnumerable<EdnsOption> GetEffectiveOptions() {
            if (PaddingLength > 0) {
                yield return new PaddingOption(PaddingLength);
            }

            if (Cookie is { Length: > 0 }) {
                yield return new CookieOption(Cookie);
            }

            foreach (EdnsOption option in Options) {
                yield return option;
            }
        }
    }
}
