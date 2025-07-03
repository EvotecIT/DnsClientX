using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class DnsCryptResolveUdp {
        internal static Task<DnsResponse> ResolveDnsCrypt(string dnsServer, int port, string providerName, string providerPublicKey, string name, DnsRecordType type, bool debug, Configuration config, CancellationToken token) {
            return DnsCrypt.QueryUdp(dnsServer, port, dnsServer, port, providerName, providerPublicKey, name, type, debug, config, token);
        }

        internal static Task<DnsResponse> ResolveDnsCryptRelay(string relayServer, int relayPort, string dnsServer, int port, string providerName, string providerPublicKey, string name, DnsRecordType type, bool debug, Configuration config, CancellationToken token) {
            // handshake happens with the resolver but packet is sent via relay
            return DnsCrypt.QueryUdp(dnsServer, port, relayServer, relayPort, providerName, providerPublicKey, name, type, debug, config, token);
        }
    }
}
