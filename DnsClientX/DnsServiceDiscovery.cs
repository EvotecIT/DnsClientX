using System.Collections.Generic;

namespace DnsClientX {
    /// <summary>
    /// Represents a discovered service using DNS-SD.
    /// </summary>
    public class DnsServiceDiscovery {
        /// <summary>
        /// The full service name, e.g. <c>_http._tcp.example.com</c>.
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Target host offering the service.
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Target port for the service.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Additional metadata from TXT records.
        /// </summary>
        public Dictionary<string, string> Txt { get; set; } = new();
    }
}
