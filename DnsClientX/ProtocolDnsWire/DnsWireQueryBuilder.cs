namespace DnsClientX {
    internal static class DnsWireQueryBuilder {
        internal static DnsMessage BuildQuery(string name, DnsRecordType type, bool requestDnsSec, Configuration cfg,
            bool? checkingDisabled = null, ushort? transactionId = null,
            System.Collections.Generic.IEnumerable<EdnsOption>? additionalOptions = null) {
            var edns = cfg.EdnsOptions;
            bool enableEdns = cfg.EnableEdns;
            int udpSize = cfg.UdpBufferSize;
            string? subnet = cfg.Subnet;
            System.Collections.Generic.IEnumerable<EdnsOption>? options = null;
            if (edns != null) {
                enableEdns = edns.EnableEdns;
                udpSize = edns.UdpBufferSize;
                subnet = edns.Subnet?.Subnet;
                options = edns.GetEffectiveOptions();
            }
            if (additionalOptions != null) {
                var combined = new System.Collections.Generic.List<EdnsOption>();
                if (options != null) combined.AddRange(options);
                combined.AddRange(additionalOptions);
                options = combined;
                enableEdns = true;
            }
            return new DnsMessage(name, type, new DnsMessageOptions(
                RequestDnsSec: requestDnsSec,
                EnableEdns: enableEdns,
                UdpBufferSize: udpSize,
                Subnet: string.IsNullOrEmpty(subnet) ? null : new EdnsClientSubnetOption(subnet!),
                CheckingDisabled: checkingDisabled ?? cfg.CheckingDisabled,
                Options: options,
                RecursionDesired: cfg.RecursionDesired,
                TransactionId: transactionId));
        }
    }
}
