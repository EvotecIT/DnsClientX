using System;

namespace DnsClientX {
    /// <summary>Contains low-allocation query completion data for application telemetry adapters.</summary>
    public sealed class DnsQueryTelemetryEventArgs : EventArgs {
        internal DnsQueryTelemetryEventArgs(string name, DnsRecordType type, DnsRequestFormat requestFormat,
            Transport? usedTransport, string? server, TimeSpan duration, DnsResponseCode? status,
            bool succeeded, Exception? exception) {
            Name = name;
            Type = type;
            RequestFormat = requestFormat;
            UsedTransport = usedTransport;
            Server = server;
            Duration = duration;
            Status = status;
            Succeeded = succeeded;
            Exception = exception;
        }

        /// <summary>Gets the caller-supplied DNS name.</summary>
        public string Name { get; }
        /// <summary>Gets the requested record type.</summary>
        public DnsRecordType Type { get; }
        /// <summary>Gets the configured request format.</summary>
        public DnsRequestFormat RequestFormat { get; }
        /// <summary>Gets the transport that produced the response, including protocol fallback.</summary>
        public Transport? UsedTransport { get; }
        /// <summary>Gets the selected resolver name or address when known.</summary>
        public string? Server { get; }
        /// <summary>Gets the total query duration, including retry and search-suffix processing.</summary>
        public TimeSpan Duration { get; }
        /// <summary>Gets the terminal DNS response code when a response was produced.</summary>
        public DnsResponseCode? Status { get; }
        /// <summary>Gets whether the query completed without a DNS, validation, or transport error.</summary>
        public bool Succeeded { get; }
        /// <summary>Gets the terminal exception when the query did not produce a response.</summary>
        public Exception? Exception { get; }
    }
}
