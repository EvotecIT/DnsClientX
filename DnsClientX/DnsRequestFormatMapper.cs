namespace DnsClientX {
    /// <summary>
    /// Maps between high-level resolver transports and concrete request formats.
    /// </summary>
    public static class DnsRequestFormatMapper {
        /// <summary>
        /// Maps a resolver transport to its default request format.
        /// </summary>
        /// <param name="transport">The high-level resolver transport.</param>
        /// <returns>The default request format for the supplied transport.</returns>
        public static DnsRequestFormat FromTransport(Transport transport) {
            return transport switch {
                Transport.Udp => DnsRequestFormat.DnsOverUDP,
                Transport.Tcp => DnsRequestFormat.DnsOverTCP,
                Transport.Dot => DnsRequestFormat.DnsOverTLS,
                Transport.Doh => DnsRequestFormat.DnsOverHttps,
                Transport.Quic => DnsRequestFormat.DnsOverQuic,
                Transport.Grpc => DnsRequestFormat.DnsOverGrpc,
                Transport.Multicast => DnsRequestFormat.Multicast,
                _ => DnsRequestFormat.DnsOverUDP
            };
        }

        /// <summary>
        /// Maps a request format to its underlying transport.
        /// </summary>
        /// <param name="requestFormat">The concrete DNS request format.</param>
        /// <returns>The transport used by the supplied request format.</returns>
        public static Transport ToTransport(DnsRequestFormat requestFormat) {
            return requestFormat switch {
                DnsRequestFormat.DnsOverUDP => Transport.Udp,
                DnsRequestFormat.DnsOverTCP => Transport.Tcp,
                DnsRequestFormat.DnsOverTLS => Transport.Dot,
                DnsRequestFormat.DnsOverQuic => Transport.Quic,
                DnsRequestFormat.DnsOverGrpc => Transport.Grpc,
                DnsRequestFormat.Multicast => Transport.Multicast,
                DnsRequestFormat.DnsOverHttps => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsJSON => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsPOST => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsWirePost => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsJSONPOST => Transport.Doh,
                DnsRequestFormat.DnsOverHttp2 => Transport.Doh,
                DnsRequestFormat.DnsOverHttp3 => Transport.Doh,
                DnsRequestFormat.ObliviousDnsOverHttps => Transport.Doh,
                _ => Transport.Udp
            };
        }
    }
}
