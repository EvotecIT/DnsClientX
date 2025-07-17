using System;

namespace DnsClientX {
    /// <summary>
    /// Represents exceptions specific to DnsClientX. This exception provides additional context
    /// by exposing the DNS response that caused the exception.
    /// </summary>
    /// <remarks>
    /// Thrown when an error occurs during resolution and the underlying response should be inspected.
    /// </remarks>
    public class DnsClientException : Exception {
        /// <summary>
        /// Gets or sets the DNS response that caused this exception.
        /// </summary>
        public DnsResponse? Response { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsClientException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public DnsClientException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsClientException"/> class with a specified error message and the DNS response that caused this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="response">The DNS response that caused this exception.</param>
        public DnsClientException(string message, DnsResponse response)
            : base(FormatMessage(message, response)) {
            Response = response;
        }

        private static string FormatMessage(string message, DnsResponse response) {
            if (response?.Questions is { Length: > 0 }) {
                var q = response.Questions[0];
                string? host = string.IsNullOrEmpty(q.HostName)
                    ? q.BaseUri?.Host
                    : q.HostName;
                if (!string.IsNullOrEmpty(host) || q.Port != 0 || q.BaseUri != null) {
                    int port = q.Port != 0 ? q.Port : q.BaseUri?.Port ?? 0;
                    string endpoint = host ?? string.Empty;
                    if (port != 0 && (q.BaseUri == null || !q.BaseUri.IsDefaultPort || q.Port != 0)) {
                        endpoint = string.Concat(endpoint, ":", port);
                    }
                    return string.Concat(message, " (Endpoint: ", endpoint, ", ", q.RequestFormat, ")");
                }
            }

            return message;
        }
    }
}
