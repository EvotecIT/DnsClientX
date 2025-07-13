using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class with filtering resolve helpers.
    /// </summary>
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
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS responses that match the filter.</returns>
        public async Task<DnsResponse[]> ResolveFilter(string[] names, DnsRecordType type, string filter, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100, CancellationToken cancellationToken = default) {
            var tasks = names.Select(name => Resolve(name, type, requestDnsSec, validateDnsSec, false, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken)).ToList();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var responses = tasks.Select(task => task.Result).ToList();

            var filteredResponses = responses
                .Where(response => HasMatchingAnswers(response.Answers ?? Array.Empty<DnsAnswer>(), filter, type))
                .Select(response => {
                    response.Answers = FilterAnswers(response.Answers, filter, type);
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
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS responses that match the filter.</returns>
        public async Task<DnsResponse[]> ResolveFilter(string[] names, DnsRecordType type, Regex regexFilter, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100, CancellationToken cancellationToken = default) {
            var tasks = names.Select(name => Resolve(name, type, requestDnsSec, validateDnsSec, false, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken)).ToList();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var responses = tasks.Select(task => task.Result).ToList();

            var filteredResponses = responses
                .Where(response => HasMatchingAnswersRegex(response.Answers ?? Array.Empty<DnsAnswer>(), regexFilter, type))
                .Select(response => {
                    response.Answers = FilterAnswersRegex(response.Answers, regexFilter, type);
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
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response that matches the filter.</returns>
        public async Task<DnsResponse> ResolveFilter(string name, DnsRecordType type, string filter, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100, CancellationToken cancellationToken = default) {
            var response = await Resolve(name, type, requestDnsSec, validateDnsSec, false, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(filter) && response.Answers != null) {
                response.Answers = FilterAnswers(response.Answers, filter, type);
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
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response that matches the filter.</returns>
        public async Task<DnsResponse> ResolveFilter(string name, DnsRecordType type, Regex regexFilter, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100, CancellationToken cancellationToken = default) {
            var response = await Resolve(name, type, requestDnsSec, validateDnsSec, false, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Answers != null) {
                response.Answers = FilterAnswersRegex(response.Answers, regexFilter, type);
            }

            return response;
        }

        /// <summary>
        /// Filters DNS answers based on a string filter, with special handling for TXT records that may contain multiple lines.
        /// </summary>
        /// <param name="answers">The DNS answers to filter.</param>
        /// <param name="filter">The filter string to search for.</param>
        /// <param name="type">The DNS record type being filtered.</param>
        /// <returns>Filtered array of DNS answers.</returns>
        private DnsAnswer[] FilterAnswers(DnsAnswer[] answers, string filter, DnsRecordType type) {
            var filteredAnswers = new List<DnsAnswer>();

            foreach (var answer in answers) {
                if (string.IsNullOrEmpty(answer.Data)) {
                    continue;
                }

                if (type == DnsRecordType.TXT && answer.Type == DnsRecordType.TXT) {
                    // For TXT records, check if any line contains the filter
                    var lines = answer.Data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var matchingLines = lines.Where(line => line.ToLower().Contains(filter.ToLower())).ToArray();

                    if (matchingLines.Length > 0) {
                        // Create a new answer with only the matching lines
                        var filteredAnswer = new DnsAnswer {
                            Name = answer.Name,
                            Type = answer.Type,
                            TTL = answer.TTL,
                            DataRaw = answer.DataRaw
                        };
                        // Override the Data property to return only matching lines
                        filteredAnswer.SetFilteredData(string.Join("\n", matchingLines));
                        filteredAnswers.Add(filteredAnswer);
                    }
                } else {
                    // For non-TXT records, use the original logic
                    if (answer.Data.ToLower().Contains(filter.ToLower())) {
                        filteredAnswers.Add(answer);
                    }
                }
            }

            return filteredAnswers.ToArray();
        }

        /// <summary>
        /// Filters DNS answers based on a regex filter, with special handling for TXT records that may contain multiple lines.
        /// </summary>
        /// <param name="answers">The DNS answers to filter.</param>
        /// <param name="regexFilter">The regex filter to match against.</param>
        /// <param name="type">The DNS record type being filtered.</param>
        /// <returns>Filtered array of DNS answers.</returns>
        private DnsAnswer[] FilterAnswersRegex(DnsAnswer[] answers, Regex regexFilter, DnsRecordType type) {
            var filteredAnswers = new List<DnsAnswer>();

            foreach (var answer in answers) {
                if (string.IsNullOrEmpty(answer.Data)) {
                    continue;
                }

                if (type == DnsRecordType.TXT && answer.Type == DnsRecordType.TXT) {
                    // For TXT records, check if any line matches the regex
                    var lines = answer.Data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var matchingLines = lines.Where(line => regexFilter.IsMatch(line)).ToArray();

                    if (matchingLines.Length > 0) {
                        // Create a new answer with only the matching lines
                        var filteredAnswer = new DnsAnswer {
                            Name = answer.Name,
                            Type = answer.Type,
                            TTL = answer.TTL,
                            DataRaw = answer.DataRaw
                        };
                        // Override the Data property to return only matching lines
                        filteredAnswer.SetFilteredData(string.Join("\n", matchingLines));
                        filteredAnswers.Add(filteredAnswer);
                    }
                } else {
                    // For non-TXT records, use the original logic
                    if (regexFilter.IsMatch(answer.Data)) {
                        filteredAnswers.Add(answer);
                    }
                }
            }

            return filteredAnswers.ToArray();
        }

        /// <summary>
        /// Checks if any answers contain matches for the given filter.
        /// </summary>
        /// <param name="answers">The DNS answers to check.</param>
        /// <param name="filter">The filter string to search for.</param>
        /// <param name="type">The DNS record type being filtered.</param>
        /// <returns>True if any answer contains a match.</returns>
        private bool HasMatchingAnswers(DnsAnswer[] answers, string filter, DnsRecordType type) {
            if (answers == null) {
                return false;
            }

            foreach (var answer in answers) {
                if (string.IsNullOrEmpty(answer.Data)) {
                    continue;
                }

                if (type == DnsRecordType.TXT && answer.Type == DnsRecordType.TXT) {
                    var lines = answer.Data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var matchingLines = lines.Where(line => line.ToLower().Contains(filter.ToLower())).ToArray();
                    if (matchingLines.Length > 0) {
                        return true;
                    }
                } else {
                    if (answer.Data.ToLower().Contains(filter.ToLower())) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if any answers contain matches for the given regex filter.
        /// </summary>
        /// <param name="answers">The DNS answers to check.</param>
        /// <param name="regexFilter">The regex filter to match against.</param>
        /// <param name="type">The DNS record type being filtered.</param>
        /// <returns>True if any answer contains a match.</returns>
        private bool HasMatchingAnswersRegex(DnsAnswer[] answers, Regex regexFilter, DnsRecordType type) {
            if (answers == null) {
                return false;
            }

            foreach (var answer in answers) {
                if (string.IsNullOrEmpty(answer.Data)) {
                    continue;
                }

                if (type == DnsRecordType.TXT && answer.Type == DnsRecordType.TXT) {
                    var lines = answer.Data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var matchingLines = lines.Where(line => regexFilter.IsMatch(line)).ToArray();
                    if (matchingLines.Length > 0) {
                        return true;
                    }
                } else {
                    if (regexFilter.IsMatch(answer.Data)) {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
