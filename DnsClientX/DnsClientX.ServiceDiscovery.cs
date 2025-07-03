using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        internal Func<string, DnsRecordType, CancellationToken, Task<DnsResponse>>? ResolverOverride;

        private Task<DnsResponse> ResolveForSd(string name, DnsRecordType type, CancellationToken cancellationToken) {
            if (ResolverOverride != null) {
                return ResolverOverride(name, type, cancellationToken);
            }

            return Resolve(name, type, requestDnsSec: false, validateDnsSec: false, returnAllTypes: false, retryOnTransient: true, maxRetries: 3, retryDelayMs: 100, cancellationToken: cancellationToken);
        }

        public async Task<DnsServiceDiscovery[]> DiscoverServices(string domain, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentNullException(nameof(domain));
            string ptrQuery = $"_services._dns-sd._udp.{domain}";
            var ptrResponse = await ResolveForSd(ptrQuery, DnsRecordType.PTR, cancellationToken);
            if (ptrResponse.Answers == null) return Array.Empty<DnsServiceDiscovery>();

            var results = new List<DnsServiceDiscovery>();
            foreach (var ptr in ptrResponse.Answers.Where(a => a.Type == DnsRecordType.PTR)) {
                string serviceDomain = ptr.Data.TrimEnd('.');
                var srvResponse = await ResolveForSd(serviceDomain, DnsRecordType.SRV, cancellationToken);
                var txtResponse = await ResolveForSd(serviceDomain, DnsRecordType.TXT, cancellationToken);

                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var txt in txtResponse.Answers?.Where(a => a.Type == DnsRecordType.TXT) ?? Array.Empty<DnsAnswer>()) {
                    foreach (string part in txt.DataStringsEscaped) {
                        var idx = part.IndexOf('=');
                        if (idx > 0) {
                            string key = part.Substring(0, idx);
                            string val = part.Substring(idx + 1);
                            metadata[key] = val;
                        } else {
                            metadata[part] = string.Empty;
                        }
                    }
                }

                foreach (var srv in srvResponse.Answers?.Where(a => a.Type == DnsRecordType.SRV) ?? Array.Empty<DnsAnswer>()) {
                    var bits = srv.Data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (bits.Length == 4 &&
                        int.TryParse(bits[0], out int priority) &&
                        int.TryParse(bits[1], out int weight) &&
                        int.TryParse(bits[2], out int port)) {
                        string target = bits[3].TrimEnd('.');
                        results.Add(new DnsServiceDiscovery {
                            ServiceName = serviceDomain,
                            Target = target,
                            Port = port,
                            Priority = priority,
                            Weight = weight,
                            Metadata = metadata.Count > 0 ? new Dictionary<string, string>(metadata) : null
                        });
                    }
                }
            }

            return results.ToArray();
        }
    }
}
