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
    /// <remarks>
    /// Instances are produced by <see cref="ClientX"/> when a query completes.
    /// </remarks>
    public class DnsResponse {
        /// <summary>
        /// Gets the transaction identifier from a DNS wire response.
        /// </summary>
        [JsonIgnore]
        public ushort TransactionId { get; internal set; }

        /// <summary>
        /// Gets whether the DNS QR flag identifies this packet as a response.
        /// </summary>
        [JsonIgnore]
        public bool IsResponse { get; internal set; }

        /// <summary>
        /// Gets the DNS operation code from the response header.
        /// </summary>
        [JsonIgnore]
        public int OperationCode { get; internal set; }

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
        /// Indicates whether the responding server is authoritative for the answer.
        /// </summary>
        [JsonPropertyName("AA")]
        public bool IsAuthoritativeAnswer { get; set; }

        /// <summary>
        /// Convenience mirror of <see cref="IsTruncated"/>.
        /// </summary>
        [JsonIgnore]
        public bool Truncated => IsTruncated;

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
        /// Gets the outcome of local DNSSEC validation. This is independent of the resolver-provided AD flag.
        /// </summary>
        [JsonPropertyName("dnssec_validation_status")]
        public DnsSecValidationStatus DnsSecValidationStatus { get; internal set; }

        /// <summary>
        /// Gets a concise explanation of the local DNSSEC validation outcome.
        /// </summary>
        [JsonPropertyName("dnssec_validation_message")]
        public string DnsSecValidationMessage { get; internal set; } = string.Empty;

        /// <summary>
        /// Gets whether DNSSEC validation was attempted locally rather than inferred from the resolver's AD flag.
        /// </summary>
        [JsonPropertyName("dnssec_validation_attempted")]
        public bool DnsSecValidationAttempted => DnsSecValidationStatus != DnsSecValidationStatus.NotRequested;

        /// <summary>
        /// Gets whether local validation proved a secure chain to a configured trust anchor.
        /// </summary>
        [JsonPropertyName("dnssec_validated_locally")]
        public bool DnsSecValidatedLocally => DnsSecValidationStatus == DnsSecValidationStatus.Secure;

        /// <summary>
        /// The questions that were asked in the DNS query. Some providers do
        /// not return the question section in their response. In those cases
        /// this property will be <c>null</c>.
        /// </summary>
        [JsonPropertyName("Question")]
        public DnsQuestion[] Questions { get; set; } = Array.Empty<DnsQuestion>();

        /// <summary>
        /// The answers provided by the DNS server.
        /// </summary>
        [JsonPropertyName("Answer")]
        public DnsAnswer[] Answers { get; set; } = Array.Empty<DnsAnswer>();

        /// <summary>
        /// Minimum TTL across <see cref="Answers"/> (seconds).
        /// </summary>
        [JsonIgnore]
        public int? TtlMin { get; internal set; }

        /// <summary>
        /// Average TTL across <see cref="Answers"/> (seconds).
        /// </summary>
        [JsonIgnore]
        public double? TtlAvg { get; internal set; }

        /// <summary>
        /// When typed parsing is enabled, contains typed representations of <see cref="Answers"/>.
        /// </summary>
        [JsonIgnore]
        public object[]? TypedAnswers { get; internal set; }

        /// <summary>
        /// Gets the answers in their minimal form.
        /// </summary>
        [JsonIgnore]
        private DnsAnswerMinimal[] _answersMinimal = Array.Empty<DnsAnswerMinimal>();

        /// <summary>
        /// Address of the DNS server that returned this response.
        /// </summary>
        [JsonIgnore]
        public string? ServerAddress { get; private set; }

        /// <summary>
        /// Transport used to obtain this response.
        /// </summary>
        [JsonIgnore]
        public Transport UsedTransport { get; internal set; }

        /// <summary>
        /// Endpoint used to obtain this response.
        /// </summary>
        [JsonIgnore]
        public DnsResolverEndpoint? UsedEndpoint { get; internal set; }

        /// <summary>
        /// Measured round-trip time for the query.
        /// </summary>
        [JsonIgnore]
        public TimeSpan RoundTripTime { get; internal set; }

        /// <summary>
        /// Normalized error code for failures.
        /// </summary>
        [JsonIgnore]
        public DnsQueryErrorCode ErrorCode { get; internal set; }

        /// <summary>
        /// Captured exception for failures (if available).
        /// </summary>
        [JsonIgnore]
        public Exception? Exception { get; internal set; }

        /// <summary>
        /// Gets the answers in their minimal form.
        /// </summary>
        [JsonIgnore]
        public DnsAnswerMinimal[] AnswersMinimal => _answersMinimal;

        /// <summary>
        /// The authority records provided by the DNS server.
        /// </summary>
        [JsonPropertyName("Authority")]
        public DnsAnswer[] Authorities { get; set; } = Array.Empty<DnsAnswer>();

        /// <summary>
        /// Any additional records provided by the DNS server.
        /// </summary>
        [JsonPropertyName("Additional")]
        public DnsAnswer[] Additional { get; set; } = Array.Empty<DnsAnswer>();

        /// <summary>
        /// An error message, if there was an issue with the DNS query. This is typically included when the HTTP status code is 400 (Bad Request).
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// An extended DNS error code message. For more information, see the <a href="https://developers.cloudflare.com/1.1.1.1/infrastructure/extended-dns-error-codes/">Cloudflare documentation</a>.
        /// </summary>
        [JsonPropertyName("Comment")]
        [JsonConverter(typeof(CommentConverter))]
        public string Comments { get; set; } = string.Empty;

        /// <summary>
        /// Extended DNS error information provided by the DNS server.
        /// </summary>
        [JsonPropertyName("extended_dns_errors")]
        public ExtendedDnsError[] ExtendedDnsErrors { get; set; } = Array.Empty<ExtendedDnsError>();

        /// <summary>
        /// Gets the extended DNS error information in a simplified form.
        /// </summary>
        [JsonIgnore]
        public ExtendedDnsErrorInfo[] ExtendedDnsErrorInfo =>
            ExtendedDnsErrors == null
                ? Array.Empty<ExtendedDnsErrorInfo>()
                : ExtendedDnsErrors
                    .Select(e => new ExtendedDnsErrorInfo { Code = e.InfoCode, Text = e.ExtraText })
                    .ToArray();


        /// <summary>
        /// The client subnet information that the DNS server used to generate the response.
        /// </summary>
        [JsonPropertyName("edns_client_subnet")]
        public string EdnsClientSubnet { get; set; } = string.Empty;

        /// <summary>
        /// Gets the UDP payload size advertised by the response OPT record, when present.
        /// </summary>
        [JsonIgnore]
        public int? EdnsUdpPayloadSize { get; internal set; }

        /// <summary>
        /// Gets the EDNS version returned by the server, when present.
        /// </summary>
        [JsonIgnore]
        public byte? EdnsVersion { get; internal set; }

        /// <summary>
        /// Gets whether the response OPT record has the DNSSEC OK flag set.
        /// </summary>
        [JsonIgnore]
        public bool EdnsDnsSecOk { get; internal set; }

        /// <summary>
        /// Gets the raw EDNS NSID option returned by the server.
        /// </summary>
        [JsonIgnore]
        public byte[] EdnsNsid { get; internal set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets the raw EDNS Cookie option returned by the server.
        /// </summary>
        [JsonIgnore]
        public byte[] EdnsCookie { get; internal set; } = Array.Empty<byte>();

        [JsonIgnore]
        internal byte[] WireMessage { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets the number of bytes in the validated wire-format response.
        /// </summary>
        [JsonIgnore]
        public int WireMessageLength => WireMessage?.Length ?? 0;

        [JsonIgnore]
        internal DnsWireResourceRecord[] WireAnswers { get; set; } = Array.Empty<DnsWireResourceRecord>();

        [JsonIgnore]
        internal DnsWireResourceRecord[] WireAuthorities { get; set; } = Array.Empty<DnsWireResourceRecord>();

        [JsonIgnore]
        internal DnsWireResourceRecord[] WireAdditional { get; set; } = Array.Empty<DnsWireResourceRecord>();

        /// <summary>
        /// Adds the server details to the DNS questions for output purposes.
        /// </summary>
        /// <param name="configuration">Client configuration used when querying.</param>
        /// <param name="usedTransport">Optional explicit transport override for cases such as UDP-to-TCP fallback.</param>
        internal void AddServerDetails(Configuration configuration, Transport? usedTransport = null) {
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
            UsedTransport = usedTransport ?? MapTransport(configuration.RequestFormat);

            RefreshDerivedData(configuration.Port, configuration.RequestFormat);
        }

        internal void RefreshDerivedData(int? port = null, DnsRequestFormat? requestFormat = null) {
            DnsAnswer[] currentAnswers = Answers ?? Array.Empty<DnsAnswer>();
            int effectivePort = port ?? (Questions != null && Questions.Length > 0 ? Questions[0].Port : 0);
            DnsRequestFormat effectiveFormat = requestFormat ??
                (Questions != null && Questions.Length > 0 ? Questions[0].RequestFormat : default);
            _answersMinimal = currentAnswers.Select(answer => new DnsAnswerMinimal {
                Name = answer.Name,
                TTL = answer.TTL,
                Type = answer.Type,
                Data = answer.Data,
                Port = effectivePort,
                RequestFormat = effectiveFormat
            }).ToArray();
            ComputeTtlMetrics();
        }

        /// <summary>
        /// Creates an independent response copy suitable for filtering or retaining beyond a cache lookup.
        /// </summary>
        public DnsResponse Clone() {
            var clone = (DnsResponse)MemberwiseClone();
            clone.Questions = Questions == null ? Array.Empty<DnsQuestion>() : (DnsQuestion[])Questions.Clone();
            clone.Answers = Answers == null ? Array.Empty<DnsAnswer>() : (DnsAnswer[])Answers.Clone();
            clone.Authorities = Authorities == null ? Array.Empty<DnsAnswer>() : (DnsAnswer[])Authorities.Clone();
            clone.Additional = Additional == null ? Array.Empty<DnsAnswer>() : (DnsAnswer[])Additional.Clone();
            clone.ExtendedDnsErrors = ExtendedDnsErrors == null ? Array.Empty<ExtendedDnsError>() : (ExtendedDnsError[])ExtendedDnsErrors.Clone();
            clone.EdnsNsid = EdnsNsid == null ? Array.Empty<byte>() : (byte[])EdnsNsid.Clone();
            clone.EdnsCookie = EdnsCookie == null ? Array.Empty<byte>() : (byte[])EdnsCookie.Clone();
            clone.TypedAnswers = TypedAnswers == null ? null : (object[])TypedAnswers.Clone();
            clone.WireMessage = WireMessage == null ? Array.Empty<byte>() : (byte[])WireMessage.Clone();
            clone.WireAnswers = WireAnswers == null ? Array.Empty<DnsWireResourceRecord>() : (DnsWireResourceRecord[])WireAnswers.Clone();
            clone.WireAuthorities = WireAuthorities == null ? Array.Empty<DnsWireResourceRecord>() : (DnsWireResourceRecord[])WireAuthorities.Clone();
            clone.WireAdditional = WireAdditional == null ? Array.Empty<DnsWireResourceRecord>() : (DnsWireResourceRecord[])WireAdditional.Clone();
            clone.RefreshDerivedData();
            return clone;
        }

        /// <summary>
        /// Creates an independent response copy with a replacement answer projection while preserving
        /// status, flags, transport, DNSSEC, EDNS, timing, and error metadata.
        /// </summary>
        /// <param name="answers">Answers to expose on the projected response.</param>
        public DnsResponse WithAnswers(IEnumerable<DnsAnswer>? answers) {
            DnsResponse clone = Clone();
            clone.Answers = answers?.ToArray() ?? Array.Empty<DnsAnswer>();
            clone.TypedAnswers = null;
            clone.RefreshDerivedData();
            return clone;
        }

        private static Transport MapTransport(DnsRequestFormat requestFormat) {
            return requestFormat switch {
                DnsRequestFormat.DnsOverUDP => Transport.Udp,
                DnsRequestFormat.DnsOverTCP => Transport.Tcp,
                DnsRequestFormat.DnsOverTLS => Transport.Dot,
                DnsRequestFormat.DnsOverQuic => Transport.Quic,
                DnsRequestFormat.DnsOverGrpc => Transport.Grpc,
                DnsRequestFormat.Multicast => Transport.Multicast,
                DnsRequestFormat.DnsOverHttps => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsJSON => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsPOST => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsWirePost => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsJSONPOST => Transport.Doh,
                DnsRequestFormat.DnsOverHttp2 => Transport.Doh,
                DnsRequestFormat.DnsOverHttp3 => Transport.Doh,
                DnsRequestFormat.ObliviousDnsOverHttps => Transport.Doh,
                _ => Transport.Udp
            };
        }

        internal void ComputeTtlMetrics() {
            try {
                if (Answers == null || Answers.Length == 0) { TtlMin = null; TtlAvg = null; return; }
                int min = int.MaxValue;
                long sum = 0;
                int count = 0;
                foreach (var a in Answers) {
                    min = a.TTL < min ? a.TTL : min;
                    sum += a.TTL;
                    count++;
                }
                if (count > 0) {
                    TtlMin = min;
                    TtlAvg = sum / (double)count;
                } else { TtlMin = null; TtlAvg = null; }
            } catch {
                // ignore TTL computation failures; keep metrics null
                TtlMin = null; TtlAvg = null;
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
                    return reader.GetString()!;
                case JsonTokenType.StartArray:
                    var comments = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
                        if (reader.TokenType == JsonTokenType.String) {
                            string? comment = reader.GetString();
                            if (comment != null) {
                                comments.Add(comment);
                            }
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
