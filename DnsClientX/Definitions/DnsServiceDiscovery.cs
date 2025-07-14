using System.Collections.Generic;

namespace DnsClientX {
    /// <summary>
    /// Represents a single service entry discovered via DNS Service Discovery.
    /// </summary>
    /// <remarks>
    /// This type models the data returned when browsing services using the <c>_services._dns-sd._udp</c> entry point.
    /// </remarks>
    public class DnsServiceDiscovery {
        /// <summary>
        /// Gets or sets the full service name returned in the PTR record,
        /// for example <c>_http._tcp.example.com</c>.
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target host of the service extracted from the SRV record.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the port number of the service.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the SRV record priority for this service instance.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the SRV record weight for this service instance.
        /// </summary>
        public int Weight { get; set; }

        /// <summary>
        /// Gets or sets additional TXT metadata associated with the service, if any.
        /// The dictionary key is the TXT record key and the value is its content.
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
