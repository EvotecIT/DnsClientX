using System;

namespace DnsClientX {
    /// <summary>
    /// Represents exceptions specific to DnsClientX. This exception provides additional context 
    /// by exposing the DNS response that caused the exception.
    /// </summary>
    public class DnsClientException : Exception {
        /// <summary>
        /// Gets or sets the DNS response that caused this exception.
        /// </summary>
        public DnsResponse Response { get; set; }

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
        public DnsClientException(string message, DnsResponse response) : base(message) {
            Response = response;
        }
    }
}
