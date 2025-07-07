using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates sending DNS queries to multiple providers in parallel.
    /// </summary>
    public class DemoResolveParallel {
        /// <summary>
        /// Executes the parallel resolve example.
        /// </summary>
        public static async Task Example() {
            var dnsEndpoints = new List<DnsEndpoint> {
                DnsEndpoint.Cloudflare,
                DnsEndpoint.CloudflareSecurity,
                DnsEndpoint.CloudflareFamily,
                DnsEndpoint.CloudflareWireFormat,
                //DnsEndpoint.CloudflareWireFormatPost,
                DnsEndpoint.Google,
                DnsEndpoint.Quad9,
                DnsEndpoint.Quad9ECS,
                DnsEndpoint.Quad9Unsecure,
                DnsEndpoint.OpenDNS,
                DnsEndpoint.OpenDNSFamily,
                DnsEndpoint.NextDNS
            };

            // List of endpoints to exclude
            var excludeEndpoints = new List<DnsEndpoint> {

            };

            var domains = new List<string> {
                "github.com",
                "microsoft.com",
                "evotec.xyz"
            };

            // List of record types to query
            var recordTypes = new List<DnsRecordType> {
                DnsRecordType.A,
                DnsRecordType.TXT,
                DnsRecordType.AAAA,
                DnsRecordType.MX,
                DnsRecordType.NS,
                DnsRecordType.SOA,
                DnsRecordType.DNSKEY,
                DnsRecordType.NSEC
            };

            var tasks = new List<Task>();
            foreach (var endpoint in dnsEndpoints) {
                if (excludeEndpoints.Contains(endpoint)) {
                    continue; // Skip this iteration if the endpoint is in the exclude list
                }

                // Create a new client for each endpoint
                using (var client = new ClientX(endpoint) {
                       Debug = false
                   }) {
                    foreach (var domain in domains) {
                        HelpersSpectre.AddLine("Resolve (Parallel)", domain, string.Join(",", recordTypes), endpoint);
                        var responses = await client.Resolve(domain, recordTypes.ToArray());
                        responses?.DisplayTable();
                    }
                }
            }
        }
    }
}
