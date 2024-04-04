using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
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
            if (res.Answers is null) return Array.Empty<DnsAnswer>();

            return res.Answers.Where(x => x.Type == type).ToArray();
        }

        /// <summary>
        /// Resolves a domain name using DNS over HTTPS and returns all answers of the provided type.
        /// This helper method is useful when you need all answers of a specific type.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="filter">Filter out results based on string. It can be helpful to filter out records such as SPF1 in TXT</param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of all DNS answers of the provided type.</returns>
        public async Task<DnsAnswer[]> ResolveAll(string name, string filter, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false) {
            DnsResponse res = await Resolve(name, type, requestDnsSec, validateDnsSec);

            // If the response is null, return an empty array
            if (res.Answers is null) return Array.Empty<DnsAnswer>();

            return res.Answers
                .Where(x => x.Type == type && x.Data.Contains(filter))
                .ToArray();
        }

        /// <summary>
        /// Resolves a domain name using DNS over HTTPS and returns all answers of the provided type.
        /// This helper method is useful when you need all answers of a specific type.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="regexPattern">Filter out results based on Regex Pattern. It can be helpful to filter out records such as SPF1 in TXT</param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of all DNS answers of the provided type.</returns>
        public async Task<DnsAnswer[]> ResolveAll(string name, Regex regexPattern, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false) {
            DnsResponse res = await Resolve(name, type, requestDnsSec, validateDnsSec);

            // If the response is null, return an empty array
            if (res.Answers is null) return Array.Empty<DnsAnswer>();

            return res.Answers
                .Where(x => x.Type == type && regexPattern.IsMatch(x.Data))
                .ToArray();
        }
    }
}
