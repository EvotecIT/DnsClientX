using System.Collections.Generic;

namespace DnsClientX {
    public class DnsServiceDiscovery {
        public string ServiceName { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public int Port { get; set; }
        public int Priority { get; set; }
        public int Weight { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
