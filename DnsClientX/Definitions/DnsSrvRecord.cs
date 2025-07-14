using System.Net;

namespace DnsClientX {
    /// <summary>
    /// Represents a SRV record optionally including resolved target addresses.
    /// </summary>
    /// <remarks>
    /// The record follows the format defined in <a href="https://www.rfc-editor.org/rfc/rfc2782">RFC 2782</a>.
    /// </remarks>
    public class DnsSrvRecord {
        /// <summary>Gets or sets the target host.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Gets or sets the service port.</summary>
        public int Port { get; set; }

        /// <summary>Gets or sets the record priority.</summary>
        public int Priority { get; set; }

        /// <summary>Gets or sets the record weight.</summary>
        public int Weight { get; set; }

        /// <summary>Gets or sets resolved IP addresses for the target, if requested.</summary>
        public IPAddress[]? Addresses { get; set; }
    }
}
