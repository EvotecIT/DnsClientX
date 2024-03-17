using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        /// <summary>
        /// Sends a DNS query for a specific record type to a DNS server.
        /// This method allows you to specify the DNS endpoint from a predefined list of endpoints.
        /// </summary>
        /// <param name="name">The domain name to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="dnsEndpoint">The DNS endpoint to use for the query. Defaults to Cloudflare.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        public static async Task<DnsResponse> QueryDns(string name, DnsRecordType recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.Cloudflare) {
            ClientX client = new ClientX(endpoint: dnsEndpoint);
            var data = await client.Resolve(name, recordType);
            return data;
        }

        /// <summary>
        /// Sends a DNS query for a specific record type to a DNS server.
        /// This method allows you to specify the DNS endpoint by providing a full URI and request format (JSON, WireFormatGet).
        /// </summary>
        /// <param name="name">The domain name to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="dnsUri">The full URI of the DNS server to query.</param>
        /// <param name="requestFormat">The format of the DNS request.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        public static async Task<DnsResponse> QueryDns(string name, DnsRecordType recordType, Uri dnsUri, DnsRequestFormat requestFormat) {
            ClientX client = new ClientX(dnsUri, requestFormat);
            var data = await client.Resolve(name, recordType);
            return data;
        }

        /// <summary>
        /// Sends a DNS query for a specific record type to a DNS server.
        /// This method allows you to specify the DNS endpoint by providing a hostname and request format (JSON, WireFormatGet).
        /// </summary>
        /// <param name="name">The domain name to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="hostName">The hostname of the DNS server to query.</param>
        /// <param name="requestFormat">The format of the DNS request.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        public static async Task<DnsResponse> QueryDns(string name, DnsRecordType recordType, string hostName, DnsRequestFormat requestFormat) {
            ClientX client = new ClientX(hostName, requestFormat);
            var data = await client.Resolve(name, recordType);
            return data;
        }

        /// <summary>
        /// Sends a DNS query to multiple domains and multiple record types to a DNS server.
        /// This method allows you to specify the DNS endpoint by providing a hostname and request format (JSON, WireFormatGet).
        /// </summary>
        /// <param name="name">Multiple domain names to check for given type</param>
        /// <param name="recordType">Multiple types to check for given name.</param>
        /// <param name="dnsEndpoint">The DNS endpoint. Default endpoint is Cloudflare</param>
        /// <returns></returns>
        public static async Task<DnsResponse[]> QueryDns(string[] name, DnsRecordType[] recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.Cloudflare) {
            ClientX client = new ClientX(endpoint: dnsEndpoint);
            var data = await client.Resolve(name, recordType);
            return data;
        }

        /// <summary>
        /// Resolves a domain name using DNS over HTTPS. This method provides full control over the output.
        /// Alternatively, <see cref="ResolveFirst"/> and <see cref="ResolveAll"/> may be used for a more streamlined experience.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
        public async Task<DnsResponse> Resolve(string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            DnsResponse response;
            if (EndpointConfiguration.RequestFormat == DnsRequestFormat.JSON) {
                response = await Client.ResolveJsonFormat(name, type, requestDnsSec, validateDnsSec, Debug);
            } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.WireFormatGet) {
                response = await Client.ResolveWireFormatGet(name, type, requestDnsSec, validateDnsSec, Debug);
            } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.WireFormatPost) {
                response = await Client.ResolveWireFormatPost(name, type, requestDnsSec, validateDnsSec, Debug);
            } else if (EndpointConfiguration.RequestFormat == DnsRequestFormat.WireFormatDot) {
                response = await DnsWireResolveDot.ResolveWireFormatDoT(name, type, requestDnsSec, validateDnsSec, Debug);
            } else {
                throw new DnsClientException($"Invalid RequestFormat: {EndpointConfiguration.RequestFormat}");
            }

            // Some DNS Providers return requested type, but also additional types for whatever reason
            // https://dns.quad9.net:5053/dns-query?name=autodiscover.evotec.pl&type=CNAME
            // We want to make sure the output is consistent
            if (!returnAllTypes && response.Answers != null) {
                response.Answers = response.Answers.Where(x => x.Type == type).ToArray();
            } else if (response.Answers == null) {
                response.Answers = Array.Empty<DnsAnswer>();
            }

            return response;
        }

        /// <summary>
        /// Resolves multiple DNS resource types for a domain name in parallel using DNS over HTTPS.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="types">The array of DNS resource record types to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of DNS responses.</returns>
        /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
        public async Task<DnsResponse[]> Resolve(string name, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false) {
            Task<DnsResponse>[] tasks = new Task<DnsResponse>[types.Length];
            for (int i = 0; i < tasks.Length; i++) {
                tasks[i] = Resolve(name, types[i], requestDnsSec, validateDnsSec);
            }

            await Task.WhenAll(tasks);

            DnsResponse[] responses = new DnsResponse[tasks.Length];

            for (int i = 0; i < tasks.Length; i++) {
                responses[i] = await tasks[i];
            }

            return responses;
        }

        /// <summary>
        /// Resolves multiple domain names for multiple DNS record types in parallel using DNS over HTTPS.
        /// </summary>
        /// <param name="names">The array of domain names to resolve.</param>
        /// <param name="types">The array of DNS resource record types to resolve.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of DNS responses.</returns>
        public async Task<DnsResponse[]> Resolve(string[] names, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false) {
            var tasks = new List<Task<DnsResponse>>();

            foreach (var name in names) {
                foreach (var type in types) {
                    tasks.Add(Resolve(name, type, requestDnsSec, validateDnsSec));
                }
            }

            await Task.WhenAll(tasks);

            return tasks.Select(task => task.Result).ToArray();
        }

        /// <summary>
        /// Resolves a domain name using DNS over HTTPS and returns the first answer of the provided type.
        /// This helper method is useful when you only need the first answer of a specific type.
        /// Alternatively, <see cref="Resolve(string, DnsRecordType, bool, bool, bool)"/> may be used to get full control over the response.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the first DNS answer of the provided type, or null if no such answer exists.</returns>
        public async Task<DnsAnswer?> ResolveFirst(string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false) {
            DnsResponse res = await Resolve(name, type, requestDnsSec, validateDnsSec);

            return res.Answers?.FirstOrDefault(x => x.Type == type);
        }

        /// <summary>
        /// Resolves a domain name using DNS over HTTPS and returns all answers of the provided type.
        /// This helper method is useful when you need all answers of a specific type.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of all DNS answers of the provided type.</returns>
        public async Task<DnsAnswer[]> ResolveAll(string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false) {
            DnsResponse res = await Resolve(name, type, requestDnsSec, validateDnsSec);

            // If the response is null, return an empty array
            // TODO: Should we throw an exception here?
            if (res.Answers is null) return Array.Empty<DnsAnswer>();

            return res.Answers.Where(x => x.Type == type).ToArray();
        }
    }
}
