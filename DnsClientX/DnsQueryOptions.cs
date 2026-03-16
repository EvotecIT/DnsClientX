using System;

namespace DnsClientX {
    /// <summary>
    /// Shared options for single-query convenience overloads.
    /// </summary>
    public sealed class DnsQueryOptions {
        /// <summary>
        /// Gets or sets the DNS endpoint to query.
        /// </summary>
        public DnsEndpoint DnsEndpoint { get; set; } = DnsEndpoint.System;

        /// <summary>
        /// Gets or sets the endpoint selection strategy.
        /// </summary>
        public DnsSelectionStrategy DnsSelectionStrategy { get; set; } = DnsSelectionStrategy.First;

        /// <summary>
        /// Gets or sets the per-query timeout in milliseconds.
        /// </summary>
        public int TimeOutMilliseconds { get; set; } = Configuration.DefaultTimeout;

        /// <summary>
        /// Gets or sets a value indicating whether transient failures should be retried.
        /// </summary>
        public bool RetryOnTransient { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retries.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay between retries in milliseconds.
        /// </summary>
        public int RetryDelayMs { get; set; } = 200;

        /// <summary>
        /// Gets or sets a value indicating whether DNSSEC data should be requested.
        /// </summary>
        public bool RequestDnsSec { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether DNSSEC validation should be performed.
        /// </summary>
        public bool ValidateDnsSec { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether typed records should be returned.
        /// </summary>
        public bool TypedRecords { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether TXT answers should be parsed into typed records.
        /// </summary>
        public bool ParseTypedTxtRecords { get; set; }

        /// <summary>
        /// Ensures option values are within valid bounds for query execution.
        /// </summary>
        public void Validate() {
            if (TimeOutMilliseconds <= 0) {
                throw new ArgumentOutOfRangeException(nameof(TimeOutMilliseconds), "TimeOutMilliseconds must be greater than zero.");
            }

            if (MaxRetries < 0) {
                throw new ArgumentOutOfRangeException(nameof(MaxRetries), "MaxRetries cannot be negative.");
            }

            if (RetryDelayMs < 0) {
                throw new ArgumentOutOfRangeException(nameof(RetryDelayMs), "RetryDelayMs cannot be negative.");
            }
        }
    }
}
