using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        private static string ExtractSpfRecord(string data) {
            if (string.IsNullOrEmpty(data)) {
                return data;
            }

            var match = Regex.Match(data, "(?i)v=spf1.*?-all");
            return match.Success ? match.Value.Trim() : data;
        }
        /// <summary>
        /// Resolves multiple domain names for a single DNS record type in parallel using DNS over HTTPS.
        /// This method allows you to specify a filter that will be applied to the data of the DNS answers.
        /// </summary>
        /// <param name="names">The domain names to resolve.</param>
        /// <param name="type">The type of DNS record to resolve.</param>
        /// <param name="filter">The filter to apply to the DNS answers data.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data in the response.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS responses that match the filter.</returns>
        public async Task<DnsResponse[]> ResolveFilter(string[] names, DnsRecordType type, string filter, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200) {
            var tasks = names.Select(name => Resolve(name, type, requestDnsSec, validateDnsSec, false, retryOnTransient, maxRetries, retryDelayMs)).ToList();

            await Task.WhenAll(tasks);

            var responses = tasks.Select(task => task.Result).ToList();

            var filteredResponses = responses
                .Where(response => response.Answers.Any(answer => answer.Data.ToLower().Contains(filter.ToLower())))
                .Select(response => {
                    response.Answers = response.Answers
                        .Where(answer => answer.Data.ToLower().Contains(filter.ToLower()))
                        .Select(answer => {
                            if (answer.Type == DnsRecordType.TXT &&
                                filter.Equals("v=spf1", System.StringComparison.OrdinalIgnoreCase)) {
                                answer.DataRaw = ExtractSpfRecord(answer.DataRaw);
                            }
                            return answer;
                        })
                        .ToArray();
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
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS responses that match the filter.</returns>
        public async Task<DnsResponse[]> ResolveFilter(string[] names, DnsRecordType type, Regex regexFilter, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200) {
            var tasks = names.Select(name => Resolve(name, type, requestDnsSec, validateDnsSec, false, retryOnTransient, maxRetries, retryDelayMs)).ToList();

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
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response that matches the filter.</returns>
        public async Task<DnsResponse> ResolveFilter(string name, DnsRecordType type, string filter, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200) {
            var response = await Resolve(name, type, requestDnsSec, validateDnsSec, false, retryOnTransient, maxRetries, retryDelayMs);

            if (!string.IsNullOrEmpty(filter) && response.Answers != null) {
                response.Answers = response.Answers
                    .Where(answer => answer.Data.ToLower().Contains(filter.ToLower()))
                    .Select(answer => {
                        if (answer.Type == DnsRecordType.TXT &&
                            filter.Equals("v=spf1", System.StringComparison.OrdinalIgnoreCase)) {
                            answer.DataRaw = ExtractSpfRecord(answer.DataRaw);
                        }
                        return answer;
                    })
                    .ToArray();
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
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response that matches the filter.</returns>
        public async Task<DnsResponse> ResolveFilter(string name, DnsRecordType type, Regex regexFilter, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200) {
            var response = await Resolve(name, type, requestDnsSec, validateDnsSec, false, retryOnTransient, maxRetries, retryDelayMs);

            if (response.Answers != null) {
                response.Answers = response.Answers
                    .Where(answer => regexFilter.IsMatch(answer.Data))
                    .Select(answer => {
                        if (answer.Type == DnsRecordType.TXT &&
                            regexFilter.IsMatch("v=spf1")) {
                            answer.DataRaw = ExtractSpfRecord(answer.DataRaw);
                        }
                        return answer;
                    })
                    .ToArray();
            }

            return response;
        }
    }
}
