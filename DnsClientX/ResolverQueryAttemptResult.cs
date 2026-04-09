using System;

namespace DnsClientX {
    /// <summary>
    /// Represents one executed resolver query attempt.
    /// </summary>
    public sealed class ResolverQueryAttemptResult {
        /// <summary>
        /// Gets or sets the human-readable target name for the attempt.
        /// </summary>
        public string Target { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the configured request format used for the attempt.
        /// </summary>
        public DnsRequestFormat RequestFormat { get; init; }

        /// <summary>
        /// Gets or sets the resolver address description used for the attempt.
        /// </summary>
        public string Resolver { get; init; } = "none";

        /// <summary>
        /// Gets or sets the DNS response returned by the attempt, when available.
        /// </summary>
        public DnsResponse? Response { get; init; }

        /// <summary>
        /// Gets or sets the elapsed duration of the attempt.
        /// </summary>
        public TimeSpan Elapsed { get; init; }

        /// <summary>
        /// Gets or sets the execution error message, when one occurred.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Gets a value indicating whether the attempt completed successfully.
        /// </summary>
        public bool Succeeded =>
            Response != null &&
            Response.Status == DnsResponseCode.NoError &&
            string.IsNullOrWhiteSpace(Response.Error);

        /// <summary>
        /// Gets the effective transport name for the attempt.
        /// </summary>
        public string Transport =>
            Response?.UsedTransport.ToString() ??
            DnsRequestFormatMapper.ToTransport(RequestFormat).ToString();

        /// <summary>
        /// Gets the response status name for the attempt.
        /// </summary>
        public string Status => Response?.Status.ToString() ?? "NoResponse";

        /// <summary>
        /// Gets the number of answer records returned by the attempt.
        /// </summary>
        public int AnswerCount => Response?.Answers?.Length ?? 0;

        /// <summary>
        /// Gets the normalized answer signature for the attempt.
        /// </summary>
        public string AnswerSignature => DnsResponseAnswerSignature.Build(Response);

        /// <summary>
        /// Gets the effective error string for the attempt.
        /// </summary>
        public string EffectiveError {
            get {
                if (!string.IsNullOrWhiteSpace(Error)) {
                    return Error!;
                }

                if (!string.IsNullOrWhiteSpace(Response?.Error)) {
                    return Response!.Error!;
                }

                if (Response != null && Response.ErrorCode != DnsQueryErrorCode.None) {
                    return Response.ErrorCode.ToString();
                }

                return "none";
            }
        }
    }
}
