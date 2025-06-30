using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        /// <summary>
        /// Resolves a domain name using DNS over HTTPS and returns the first answer of the provided type.
        /// This helper method is useful when you only need the first answer of a specific type.
        /// Alternatively, <see cref="Resolve(string, DnsRecordType, bool, bool, bool, bool, int, int)"/> may be used to get full control over the response.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the first DNS answer of the provided type, or null if no such answer exists.</returns>
        public async Task<DnsAnswer?> ResolveFirst(string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100) {
            DnsResponse res = await Resolve(
                name,
                type,
                requestDnsSec,
                validateDnsSec,
                returnAllTypes: false,
                retryOnTransient: retryOnTransient,
                maxRetries: maxRetries,
                retryDelayMs: retryDelayMs).ConfigureAwait(false);

            return res.Answers?.FirstOrDefault(x => x.Type == type);
        }

        /// <summary>
        /// Resolves a domain name using DNS over HTTPS and returns the first answer of the provided type. Synchronous version.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <returns>The first DNS answer of the provided type, or null if no such answer exists.</returns>
        public DnsAnswer? ResolveFirstSync(string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100) {
            return ResolveFirst(name, type, requestDnsSec, validateDnsSec, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();
        }
    }
}
