using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        /// <summary>
        /// Resolves multiple domain names for a single DNS record type in parallel using DNS over HTTPS.
        /// This method allows you to specify a filter that will be applied to the data of the DNS answers.
        /// </summary>
        /// <param name="names">The domain names to resolve.</param>
        /// <param name="type">The type of DNS record to resolve.</param>
        /// <param name="filter">The filter to apply to the DNS answers data.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS responses that match the filter.</returns>
        public async Task<DnsResponse[]> ResolveFilter(string[] names, DnsRecordType type, string filter, bool requestDnsSec = false, bool validateDnsSec = false) {
            var tasks = names.Select(name => Resolve(name, type, requestDnsSec, validateDnsSec)).ToList();

            await Task.WhenAll(tasks);

            var responses = tasks.Select(task => task.Result).ToList();

            var filteredResponses = responses
                .Where(response => response.Answers.Any(answer => answer.Data.ToLower().Contains(filter.ToLower())))
                .Select(response => {
                    response.Answers = response.Answers.Where(answer => answer.Data.ToLower().Contains(filter.ToLower())).ToArray();
                    return response;
                })
                .ToArray();

            return filteredResponses;
        }

        /// <summary>
        /// Resolves multiple domain names for a single DNS record type in parallel using DNS over HTTPS.
        /// This method allows you to specify a regular expression filter that will be applied to the data of the DNS answers.
        /// </summary>
        /// <param name="names">The domain names to resolve.</param>
        /// <param name="type">The type of DNS record to resolve.</param>
        /// <param name="regexFilter">The regular expression filter to apply to the DNS answers data.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS responses that match the filter.</returns>
        public async Task<DnsResponse[]> ResolveFilter(string[] names, DnsRecordType type, Regex regexFilter, bool requestDnsSec = false, bool validateDnsSec = false) {
            var tasks = names.Select(name => Resolve(name, type, requestDnsSec, validateDnsSec)).ToList();

            await Task.WhenAll(tasks);

            var responses = tasks.Select(task => task.Result).ToList();

            var filteredResponses = responses
                .Where(response => response.Answers.Any(answer => regexFilter.IsMatch(answer.Data)))
                .Select(response => {
                    response.Answers = response.Answers.Where(answer => regexFilter.IsMatch(answer.Data)).ToArray();
                    return response;
                })
                .ToArray();

            return filteredResponses;
        }


        /// <summary>
        /// Resolves a single domain name for a single DNS record type using DNS over HTTPS.
        /// This method allows you to specify a filter that will be applied to the data of the DNS answers.
        /// </summary>
        /// <param name="name">The domain name to resolve.</param>
        /// <param name="type">The type of DNS record to resolve.</param>
        /// <param name="filter">The filter to apply to the DNS answers data.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response that matches the filter.</returns>
        public async Task<DnsResponse> ResolveFilter(string name, DnsRecordType type, string filter, bool requestDnsSec = false, bool validateDnsSec = false) {

            var response = await Resolve(name, type, requestDnsSec, validateDnsSec);

            if (!string.IsNullOrEmpty(filter) && response.Answers != null) {
                response.Answers = response.Answers.Where(answer => answer.Data.ToLower().Contains(filter.ToLower())).ToArray();
            }

            return response;
        }

        /// <summary>
        /// Resolves a single domain name for a single DNS record type using DNS over HTTPS.
        /// This method allows you to specify a regular expression filter that will be applied to the data of the DNS answers.
        /// </summary>
        /// <param name="name">The domain name to resolve.</param>
        /// <param name="type">The type of DNS record to resolve.</param>
        /// <param name="regexFilter">The regular expression filter to apply to the DNS answers data.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response that matches the filter.</returns>
        public async Task<DnsResponse> ResolveFilter(string name, DnsRecordType type, Regex regexFilter,
            bool requestDnsSec = false, bool validateDnsSec = false) {
            var response = await Resolve(name, type, requestDnsSec, validateDnsSec);

            if (response.Answers != null) {
                response.Answers = response.Answers.Where(answer => regexFilter.IsMatch(answer.Data)).ToArray();
            }

            return response;
        }
    }
}
