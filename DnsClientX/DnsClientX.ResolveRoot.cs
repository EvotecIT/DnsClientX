using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class implementing root server resolution logic.
    /// </summary>
    /// <remarks>
    /// These methods directly query the root DNS servers and follow referrals to authoritative servers.
    /// </remarks>
    public partial class ClientX {
        /// <summary>
        /// Resolves a domain name by iteratively querying root servers
        /// and following NS referrals until an answer is obtained.
        /// </summary>
        /// <param name="name">Domain name to resolve.</param>
        /// <param name="type">Record type to resolve.</param>
        /// <param name="servers">Optional list of root servers to query.</param>
        /// <param name="maxRetries">Maximum referral retries.</param>
        /// <param name="port">Port used to query each server.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        public async Task<DnsResponse> ResolveFromRoot(
            string name,
            DnsRecordType type = DnsRecordType.A,
            IEnumerable<string>? servers = null,
            int maxRetries = 10,
            int port = 53,
            CancellationToken cancellationToken = default) {
            var serverList = (servers ?? RootServers.Servers).ToArray();
            DnsResponse lastResponse = new();
            for (var depth = 0; depth < maxRetries; depth++) {
                foreach (var server in serverList) {
                    var host = server.TrimEnd('.');
                    var cfg = new Configuration(host, DnsRequestFormat.DnsOverUDP) { UseTcpFallback = true, Port = port };
                    lastResponse = await DnsWireResolveUdp.ResolveWireFormatUdp(host, cfg.Port, name, type, false, false, Debug, cfg, 1, cancellationToken).ConfigureAwait(false);
                    if (lastResponse.Answers?.Any(a => a.Type == type) == true) {
                        return lastResponse;
                    }
                }

                var next = lastResponse.Additional?
                    .Where(a => a.Type == DnsRecordType.A || a.Type == DnsRecordType.AAAA)
                    .Select(a => a.Data.TrimEnd('.'))
                    .ToArray();
                if (next != null && next.Length > 0) {
                    serverList = next;
                    continue;
                }
                var ns = lastResponse.Authorities?
                    .Where(a => a.Type == DnsRecordType.NS)
                    .Select(a => a.Data.TrimEnd('.'))
                    .FirstOrDefault();
                if (ns == null) {
                    return lastResponse;
                }
                var nsResponse = await ResolveFromRoot(ns, DnsRecordType.A, servers ?? serverList, maxRetries, port, cancellationToken).ConfigureAwait(false);
                serverList = nsResponse.Answers?.Select(a => a.Data.TrimEnd('.')).ToArray() ?? RootServers.Servers;
            }
            return lastResponse;
        }
    }
}
