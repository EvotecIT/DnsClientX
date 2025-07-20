namespace DnsClientX {
    internal static class DnsWireQueryBuilder {
        internal static DnsMessage BuildQuery(string name, DnsRecordType type, bool requestDnsSec, Configuration cfg) {
            var edns = cfg.EdnsOptions;
            bool enableEdns = cfg.EnableEdns;
            int udpSize = cfg.UdpBufferSize;
            string? subnet = cfg.Subnet;
            System.Collections.Generic.IEnumerable<EdnsOption>? options = null;
            if (edns != null) {
                enableEdns = edns.EnableEdns;
                udpSize = edns.UdpBufferSize;
                subnet = edns.Subnet?.Subnet;
                options = edns.Options;
            }
            return new DnsMessage(name, type, requestDnsSec, enableEdns, udpSize, subnet,
                cfg.CheckingDisabled, cfg.SigningKey, options);
        }
    }
}
