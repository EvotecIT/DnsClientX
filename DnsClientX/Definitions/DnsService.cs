using System.Collections.Generic;

namespace DnsClientX {
    /// <summary>
    /// Represents a service instance discovered via DNS-SD.
    /// </summary>
    /// <remarks>
    /// The information contained in this class matches data returned by the PTR and SRV records used in DNS Service Discovery.
    /// </remarks>
    public class DnsService {
        /// <summary>Gets or sets the full PTR service name.</summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>Gets or sets the target host of the service.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Gets or sets the service port.</summary>
        public int Port { get; set; }

        /// <summary>Gets or sets the SRV record priority.</summary>
        public int Priority { get; set; }

        /// <summary>Gets or sets the SRV record weight.</summary>
        public int Weight { get; set; }

        /// <summary>Gets or sets optional TXT metadata associated with the service.</summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
