using Spectre.Console;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates resolving records from all configured DNS providers.
    /// </summary>
    public static class DemoResolveAll {
        /// <summary>
        /// Executes the resolve-all example.
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
                            HelpersSpectre.AddLine("ResolveAll", domain, recordType, endpoint);
                            var response = await client.ResolveAll(domain, recordType);
                            response.DisplayToConsole();
                        }
                    }
                }
            }
        }

        public static async Task Example2() {
            var dnsEndpoints = new List<DnsEndpoint> {
                DnsEndpoint.Cloudflare,
            };

            // List of endpoints to exclude
            var excludeEndpoints = new List<DnsEndpoint> {

            };

            var domains = new List<string> {
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
                            HelpersSpectre.AddLine("ResolveAll", domain, recordType, endpoint);
                            var response = await client.ResolveAll(domain, recordType);
                            response.DisplayToConsole();
                        }
                    }
                }
            }
        }
    }
}
