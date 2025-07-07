using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates filtering DNS TXT records across multiple providers.
    /// </summary>
    internal class DemoResolveWithFilter {
        /// <summary>
        /// Executes the filtered resolve example.
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

            string filter = "SPF1";

            // List of record types to query
            var recordTypes = new List<DnsRecordType> {
                DnsRecordType.TXT,
            };

            foreach (var endpoint in dnsEndpoints) {
                if (excludeEndpoints.Contains(endpoint)) {
                    continue; // Skip this iteration if the endpoint is in the exclude list
                }

                // Create a new client for each endpoint
                using (var client = new ClientX(endpoint, DnsSelectionStrategy.Random) {
                       Debug = false
                   }) {
                    foreach (var domain in domains) {
                        foreach (var recordType in recordTypes) {
                            HelpersSpectre.AddLine("Resolve", domain, recordType, endpoint);
                            DnsResponse? response = await client.ResolveFilter(domain, recordType, filter);
                            response?.DisplayTable();
                        }
                    }
                }
            }
        }

        public static async Task ExampleMultipleNames() {
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

            var domains = new[] {
                "github.com",
                "microsoft.com",
                "evotec.xyz"
            };

            string filter = "v=spf1";

            // List of record types to query
            var recordType = DnsRecordType.TXT;


            foreach (var endpoint in dnsEndpoints) {
                if (excludeEndpoints.Contains(endpoint)) {
                    continue; // Skip this iteration if the endpoint is in the exclude list
                }

                // Create a new client for each endpoint
                using (var client = new ClientX(endpoint, DnsSelectionStrategy.Random) {
                       Debug = false
                   }) {
                    HelpersSpectre.AddLine("Resolve", "Multiple Domains", recordType, endpoint);
                    var response = await client.ResolveFilter(domains, recordType, filter);
                    response?.DisplayTable();
                }
            }
        }
    }
}
