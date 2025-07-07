using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoResolveReturn {
        /// <summary>
        /// Shows a demo for Resolve method when using returnAllTypes
        /// By default we try to limit answer to what was requested
        /// But DNS tries to be helpful in some cases and returns additional records
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
                "google.com",
                "autodiscover.evotec.pl",
                "www.microsoft.com",
            };

            // List of record types to query
            var recordTypes = new List<DnsRecordType> {
                DnsRecordType.A,
                DnsRecordType.CNAME,
                DnsRecordType.TXT,
                DnsRecordType.AAAA,
                DnsRecordType.MX,
                DnsRecordType.NS,
                DnsRecordType.SOA,
                DnsRecordType.DNSKEY,
                DnsRecordType.NSEC
            };

            foreach (var endpoint in dnsEndpoints) {
                if (excludeEndpoints.Contains(endpoint)) {
                    continue; // Skip this iteration if the endpoint is in the exclude list
                }

                // Create a new client for each endpoint
                using (var client = new ClientX(endpoint) {
                       Debug = false
                   }) {
                    foreach (var domain in domains) {
                        foreach (var recordType in recordTypes) {
                            HelpersSpectre.AddLine("Resolve", domain, recordType, endpoint);
                            DnsResponse? response = await client.Resolve(domain, recordType, requestDnsSec: true, validateDnsSec: true, returnAllTypes: true);
                            response?.DisplayTable();
                        }
                    }
                }
            }
        }
    }
}
