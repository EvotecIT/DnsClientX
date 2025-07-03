using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        /// <summary>
        /// Discovers services using DNS Service Discovery (DNS-SD).
        /// </summary>
        /// <param name="domain">The domain to query.</param>
        /// <returns>List of discovered services.</returns>
        public async Task<DnsServiceDiscovery[]> DiscoverServices(string domain) {
            if (string.IsNullOrWhiteSpace(domain)) {
                throw new ArgumentNullException(nameof(domain));
            }

            var list = new List<DnsServiceDiscovery>();
            string ptrName = $"_services._dns-sd._udp.{domain}";
            var ptrResponse = await Resolve(ptrName, DnsRecordType.PTR, retryOnTransient: false);
            var services = ptrResponse.Answers?.Where(a => a.Type == DnsRecordType.PTR) ?? Array.Empty<DnsAnswer>();

            foreach (var service in services) {
                string serviceName = service.Data.TrimEnd('.');
                var srv = await ResolveFirst(serviceName, DnsRecordType.SRV, retryOnTransient: false);
                var txt = await ResolveFirst(serviceName, DnsRecordType.TXT, retryOnTransient: false);

                string host = string.Empty;
                int port = 0;
                if (srv.HasValue) {
                    var parts = srv.Value.Data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4) {
                        int.TryParse(parts[2], out port);
                        host = parts[3].TrimEnd('.');
                    }
                }

                var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (txt.HasValue) {
                    foreach (var entry in txt.Value.DataStringsEscaped) {
                        var idx = entry.IndexOf('=');
                        if (idx > 0) {
                            meta[entry[..idx]] = entry[(idx + 1)..];
                        } else {
                            meta[entry] = string.Empty;
                        }
                    }
                }

                list.Add(new DnsServiceDiscovery {
                    ServiceName = serviceName,
                    Host = host,
                    Port = port,
                    Txt = meta
                });
            }

            return list.ToArray();
        }
    }
}
