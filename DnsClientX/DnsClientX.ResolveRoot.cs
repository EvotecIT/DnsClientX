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
                if (cancellationToken.IsCancellationRequested) {
                    return CreateCancelledRootResponse(name, type, depth, lastResponse);
                }
                foreach (var server in serverList) {
                    if (cancellationToken.IsCancellationRequested) {
                        return CreateCancelledRootResponse(name, type, depth, lastResponse);
                    }
                    var host = server.TrimEnd('.');
                    var cfg = new Configuration(host, DnsRequestFormat.DnsOverUDP) { UseTcpFallback = true, Port = port };
                    lastResponse = await DnsWireResolveUdp.ResolveWireFormatUdp(host, cfg.Port, name, type, false, false, Debug, cfg, 1, cancellationToken).ConfigureAwait(false);
                    if (lastResponse.Answers?.Any(a => a.Type == type) == true) {
                        lastResponse.RetryCount = depth;
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
                    lastResponse.RetryCount = depth;
                    return lastResponse;
                }
                int remaining = maxRetries - depth - 1;
                if (remaining <= 0) {
                    lastResponse.RetryCount = depth;
                    return lastResponse;
                }
                if (cancellationToken.IsCancellationRequested) {
                    return CreateCancelledRootResponse(name, type, depth, lastResponse);
                }
                var nsResponse = await ResolveFromRoot(ns, DnsRecordType.A, servers ?? serverList, remaining, port, cancellationToken).ConfigureAwait(false);
                serverList = nsResponse.Answers?.Select(a => a.Data.TrimEnd('.')).ToArray() ?? serverList;
            }
            lastResponse.RetryCount = maxRetries - 1;
            return lastResponse;
        }

        private static DnsResponse CreateCancelledRootResponse(string name, DnsRecordType type, int retryCount, DnsResponse? lastResponse = null) {
            var response = lastResponse ?? new DnsResponse();
            response.Status = response.Status == DnsResponseCode.NoError ? DnsResponseCode.ServerFailure : response.Status;
            response.Error ??= "Operation canceled.";
            response.RetryCount = retryCount;
            response.Questions ??= [
                new DnsQuestion {
                    Name = name,
                    RequestFormat = DnsRequestFormat.DnsOverUDP,
                    Type = type,
                    OriginalName = name
                }
            ];
            return response;
        }
    }
}
