using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        /// <summary>
        /// Resolves a domain name by iteratively querying root servers
        /// and following NS referrals until an answer is obtained.
        /// </summary>
        public async Task<DnsResponse> ResolveFromRoot(string name, DnsRecordType type = DnsRecordType.A, CancellationToken cancellationToken = default) {
            var servers = RootServers.Servers.ToArray();
            DnsResponse lastResponse = new();
            for (var depth = 0; depth < 10; depth++) {
                foreach (var server in servers) {
                    var host = server.TrimEnd('.');
                    var cfg = new Configuration(host, DnsRequestFormat.DnsOverUDP) { UseTcpFallback = true };
                    lastResponse = await DnsWireResolveUdp.ResolveWireFormatUdp(host, cfg.Port, name, type, false, false, Debug, cfg, cancellationToken).ConfigureAwait(false);
                    if (lastResponse.Answers?.Any(a => a.Type == type) == true) {
                        return lastResponse;
                    }
                }

                var next = lastResponse.Additional?
                    .Where(a => a.Type == DnsRecordType.A || a.Type == DnsRecordType.AAAA)
                    .Select(a => a.Data.TrimEnd('.'))
                    .ToArray();
                if (next != null && next.Length > 0) {
                    servers = next;
                    continue;
                }
                var ns = lastResponse.Authorities?
                    .Where(a => a.Type == DnsRecordType.NS)
                    .Select(a => a.Data.TrimEnd('.'))
                    .FirstOrDefault();
                if (ns == null) {
                    return lastResponse;
                }
                var nsResponse = await ResolveFromRoot(ns, DnsRecordType.A, cancellationToken).ConfigureAwait(false);
                servers = nsResponse.Answers?.Select(a => a.Data.TrimEnd('.')).ToArray() ?? RootServers.Servers;
            }
            return lastResponse;
        }
    }
}
