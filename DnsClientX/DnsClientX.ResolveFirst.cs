using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class with helpers for resolving first matching records.
    /// </summary>
    /// <remarks>
    /// These convenience methods return only the first answer for scenarios where only one result is expected.
    /// </remarks>
    public partial class ClientX {
        /// <summary>
        /// Resolves a domain name using DNS over HTTPS and returns the first answer of the provided type.
        /// This helper method is useful when you only need the first answer of a specific type.
        /// Alternatively, <see cref="ClientX.Resolve(string, DnsRecordType, bool, bool, bool, bool, int, int, bool, bool, CancellationToken)"/> may be used to get full control over the response.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the first DNS answer of the provided type, or null if no such answer exists.</returns>
        public async Task<DnsAnswer?> ResolveFirst(string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool typedRecords = false, bool parseTypedTxtRecords = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100, CancellationToken cancellationToken = default) {
            DnsResponse res = await Resolve(
                name,
                type,
                requestDnsSec,
                validateDnsSec,
                returnAllTypes: false,
                typedRecords: typedRecords,
                parseTypedTxtRecords: parseTypedTxtRecords,
                retryOnTransient: retryOnTransient,
                maxRetries: maxRetries,
                retryDelayMs: retryDelayMs,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (res.Answers == null || res.Answers.Length == 0) {
                return null;
            }

            foreach (DnsAnswer answer in res.Answers) {
                if (answer.Type == type) {
                    return answer;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a domain name using DNS over HTTPS and returns the first answer of the provided type. Synchronous version.
        /// </summary>
        /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
        /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="typedRecords">Return answers as typed records.</param>
        /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The first DNS answer of the provided type, or null if no such answer exists.</returns>
        public DnsAnswer? ResolveFirstSync(string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool typedRecords = false, bool parseTypedTxtRecords = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100, CancellationToken cancellationToken = default) {
            return ResolveFirst(name, type, requestDnsSec, validateDnsSec, typedRecords, parseTypedTxtRecords, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).RunSync();
        }
    }
}
