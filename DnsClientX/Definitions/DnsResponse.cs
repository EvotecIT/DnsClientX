using System.Linq;
using System.Text.Json.Serialization;

namespace DnsClientX {
    /// <summary>
    /// Represents the response from a DNS query over HTTPS. This can be in either JSON or wire format.
    /// </summary>
    public struct DnsResponse {
        /// <summary>
        /// The status code of the DNS response.
        /// </summary>
        [JsonPropertyName("Status")]
        public DnsResponseCode Status { get; internal set; }

        /// <summary>
        /// Indicates whether the response was truncated. This can occur if the response is larger than the maximum size allowed by the transmission channel.
        /// This is typically false for DNS over HTTPS, as most providers support the maximum response size.
        /// </summary>
        [JsonPropertyName("TC")]
        public bool IsTruncated { get; internal set; }

        /// <summary>
        /// Indicates whether recursion was requested in the query. This is typically true for DNS over HTTPS.
        /// </summary>
        [JsonPropertyName("RD")]
        public bool IsRecursionDesired { get; internal set; }

        /// <summary>
        /// Indicates whether recursion is available on the DNS server. This is typically true for DNS over HTTPS.
        /// </summary>
        [JsonPropertyName("RA")]
        public bool IsRecursionAvailable { get; internal set; }

        /// <summary>
        /// Indicates whether the DNS server believes the response is authentic and has been validated by DNSSEC.
        /// </summary>
        [JsonPropertyName("AD")]
        public bool AuthenticData { get; internal set; }

        /// <summary>
        /// Indicates whether a security-aware resolver should disable DNSSEC signature validation.
        /// </summary>
        [JsonPropertyName("CD")]
        public bool CheckingDisabled { get; internal set; }

        /// <summary>
        /// The questions that were asked in the DNS query.
        /// </summary>
        [JsonPropertyName("Question")]
        public DnsQuestion[] Questions { get; internal set; }

        /// <summary>
        /// The answers provided by the DNS server.
        /// </summary>
        [JsonPropertyName("Answer")]
        public DnsAnswer[] Answers { get; internal set; }

        /// <summary>
        /// Gets the answers in their minimal form
        /// </summary>
        public DnsAnswerMinimal[] AnswersMinimal => Answers.Select(answer => (DnsAnswerMinimal)answer).ToArray();

        /// <summary>
        /// The authority records provided by the DNS server.
        /// </summary>
        [JsonPropertyName("Authority")]
        public DnsAnswer[] Authorities { get; internal set; }

        /// <summary>
        /// Any additional records provided by the DNS server.
        /// </summary>
        [JsonPropertyName("Additional")]
        public DnsAnswer[] Additional { get; internal set; }

        /// <summary>
        /// An error message, if there was an issue with the DNS query. This is typically included when the HTTP status code is 400 (Bad Request).
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; internal set; }

        /// <summary>
        /// An extended DNS error code message. For more information, see the <a href="https://developers.cloudflare.com/1.1.1.1/infrastructure/extended-dns-error-codes/">Cloudflare documentation</a>.
        /// </summary>
        [JsonPropertyName("Comment")]
        public string Comments { get; internal set; }
    }
}
