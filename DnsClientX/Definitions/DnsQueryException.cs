using System;

namespace DnsClientX {
    /// <summary>
    /// Wraps network/parse/timeout exceptions during DNS querying with a normalized error code.
    /// </summary>
    public sealed class DnsQueryException : Exception {
        /// <summary>
        /// Normalized error category.
        /// </summary>
        public DnsQueryErrorCode ErrorCode { get; }
        /// <summary>
        /// Optional response associated with the failure.
        /// </summary>
        public DnsResponse? Response { get; }

        /// <summary>
        /// Creates a new instance with the specified error category and message.
        /// </summary>
        public DnsQueryException(DnsQueryErrorCode errorCode, string message, Exception? inner = null, DnsResponse? response = null)
            : base(message, inner) {
            ErrorCode = errorCode;
            Response = response;
        }
    }
}
