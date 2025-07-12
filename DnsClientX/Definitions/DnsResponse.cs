using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DnsClientX {
    /// <summary>
    /// Represents a DNS message returned by a resolver.
    /// The structure mirrors the response format described in
    /// <a href="https://www.rfc-editor.org/rfc/rfc1035">RFC 1035</a>.
    /// </summary>
    public class DnsResponse {
        /// <summary>
        /// The status code of the DNS response.
        /// </summary>
        [JsonPropertyName("Status")]
        public DnsResponseCode Status { get; set; }

        /// <summary>
        /// Number of retry attempts performed before receiving this response.
        /// </summary>
        [JsonIgnore]
        public int RetryCount { get; internal set; }

        /// <summary>
        /// Indicates whether the response was truncated. This can occur if the response is larger than the maximum size allowed by the transmission channel.
        /// This is typically false for DNS over HTTPS, as most providers support the maximum response size.
        /// </summary>
        [JsonPropertyName("TC")]
        public bool IsTruncated { get; set; }

        /// <summary>
        /// Indicates whether recursion was requested in the query. This is typically true for DNS over HTTPS.
        /// </summary>
        [JsonPropertyName("RD")]
        public bool IsRecursionDesired { get; set; }

        /// <summary>
        /// Indicates whether recursion is available on the DNS server. This is typically true for DNS over HTTPS.
        /// </summary>
        [JsonPropertyName("RA")]
        public bool IsRecursionAvailable { get; set; }

        /// <summary>
        /// Indicates whether the DNS server believes the response is authentic and has been validated by DNSSEC.
        /// </summary>
        [JsonPropertyName("AD")]
        public bool AuthenticData { get; set; }

        /// <summary>
        /// Indicates whether a security-aware resolver should disable DNSSEC signature validation.
        /// </summary>
        [JsonPropertyName("CD")]
        public bool CheckingDisabled { get; set; }

        /// <summary>
        /// The questions that were asked in the DNS query. Some providers do
        /// not return the question section in their response. In those cases
        /// this property will be <c>null</c>.
        /// </summary>
        [JsonPropertyName("Question")]
        public DnsQuestion[] Questions { get; set; }

        /// <summary>
        /// The answers provided by the DNS server.
        /// </summary>
        [JsonPropertyName("Answer")]
        public DnsAnswer[] Answers { get; set; }

        /// <summary>
        /// Gets the answers in their minimal form.
        /// </summary>
        [JsonIgnore]
        private DnsAnswerMinimal[] _answersMinimal;

        /// <summary>
        /// Address of the DNS server that returned this response.
        /// </summary>
        [JsonIgnore]
        public string? ServerAddress { get; private set; }

        /// <summary>
        /// Gets the answers in their minimal form.
        /// </summary>
        [JsonIgnore]
        public DnsAnswerMinimal[] AnswersMinimal => _answersMinimal ?? Answers.Select(answer => (DnsAnswerMinimal)answer).ToArray();

        /// <summary>
        /// The authority records provided by the DNS server.
        /// </summary>
        [JsonPropertyName("Authority")]
        public DnsAnswer[] Authorities { get; set; }

        /// <summary>
        /// Any additional records provided by the DNS server.
        /// </summary>
        [JsonPropertyName("Additional")]
        public DnsAnswer[] Additional { get; set; }

        /// <summary>
        /// An error message, if there was an issue with the DNS query. This is typically included when the HTTP status code is 400 (Bad Request).
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; }

        /// <summary>
        /// An extended DNS error code message. For more information, see the <a href="https://developers.cloudflare.com/1.1.1.1/infrastructure/extended-dns-error-codes/">Cloudflare documentation</a>.
        /// </summary>
        [JsonPropertyName("Comment")]
        [JsonConverter(typeof(CommentConverter))]
        public string Comments { get; set; }

        /// <summary>
        /// Extended DNS error information provided by the DNS server.
        /// </summary>
        [JsonPropertyName("extended_dns_errors")]
        public ExtendedDnsError[] ExtendedDnsErrors { get; set; }


        /// <summary>
        /// The client subnet information that the DNS server used to generate the response.
        /// </summary>
        [JsonPropertyName("edns_client_subnet")]
        public string EdnsClientSubnet { get; set; }

        /// <summary>
        /// Adds the server details to the DNS questions for output purposes.
        /// </summary>
        /// <param name="configuration">Client configuration used when querying.</param>
        internal void AddServerDetails(Configuration configuration) {
            if (Questions == null) {
                Questions = Array.Empty<DnsQuestion>();
            }
            for (int i = 0; i < Questions.Length; i++) {
                Questions[i].HostName = configuration.Hostname;
                if (configuration.BaseUri != null) {
                    Questions[i].BaseUri = configuration.BaseUri;
                }
                Questions[i].RequestFormat = configuration.RequestFormat;
                Questions[i].Port = configuration.Port;
            }

            ServerAddress = configuration.Hostname;

            if (Answers != null) {
                _answersMinimal = Answers.Select(answer => new DnsAnswerMinimal {
                    Name = answer.Name,
                    TTL = answer.TTL,
                    Type = answer.Type,
                    Data = answer.Data,
                    Port = configuration.Port,
                    RequestFormat = configuration.RequestFormat
                }).ToArray();
            }
        }
    }

    /// <summary>
    /// Extended DNS error information provided by the DNS server.
    /// Googles documentation: https://developers.google.com/speed/public-dns/docs/dns-over-https#extended_dns_errors
    /// </summary>
    public struct ExtendedDnsError {
        /// <summary>
        /// The extended DNS error information code.
        /// </summary>
        [JsonPropertyName("info_code")]
        public int InfoCode { get; set; }

        /// <summary>
        /// Additional text providing more details about the error.
        /// </summary>
        [JsonPropertyName("extra_text")]
        public string ExtraText { get; set; }
    }

    /// <summary>
    /// Converts the comment field in the JSON response to a string.
    /// In some cases the comment field is an array of strings, so this converter will join them together.
    /// </summary>
    public class CommentConverter : JsonConverter<string> {
        /// <inheritdoc />
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            switch (reader.TokenType) {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.StartArray:
                    var comments = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
                        if (reader.TokenType == JsonTokenType.String) {
                            comments.Add(reader.GetString());
                        }
                    }
                    return string.Join("; ", comments);
                default:
                    throw new JsonException();
            }
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) {
            writer.WriteStringValue(value);
        }
    }
}
