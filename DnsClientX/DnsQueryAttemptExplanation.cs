using System;

namespace DnsClientX {
    /// <summary>Provides JSON-safe facts for an audit attempt without serializing exceptions.</summary>
    public sealed class DnsQueryAttemptExplanation {
        /// <summary>Gets the query name.</summary>
        public string Name { get; private set; } = string.Empty;
        /// <summary>Gets the query type.</summary>
        public DnsRecordType RecordType { get; private set; }
        /// <summary>Gets the attempt start time.</summary>
        public DateTimeOffset StartedAtUtc { get; private set; }
        /// <summary>Gets the duration in milliseconds.</summary>
        public double ElapsedMs { get; private set; }
        /// <summary>Gets the selected resolver.</summary>
        public string? Resolver { get; private set; }
        /// <summary>Gets the port.</summary>
        public int Port { get; private set; }
        /// <summary>Gets the request format.</summary>
        public DnsRequestFormat RequestFormat { get; private set; }
        /// <summary>Gets the actual transport.</summary>
        public Transport Transport { get; private set; }
        /// <summary>Gets whether cache supplied the response.</summary>
        public bool ServedFromCache { get; private set; }
        /// <summary>Gets the captured DNS status, absent if no response arrived.</summary>
        public DnsResponseCode? Status { get; private set; }
        /// <summary>Gets the reason for retrying, when captured.</summary>
        public string? RetryReason { get; private set; }
        /// <summary>Gets the captured error message, absent on success.</summary>
        public string? Error { get; private set; }

        internal static DnsQueryAttemptExplanation Create(AuditEntry entry) => new DnsQueryAttemptExplanation {
            Name = entry.Name, RecordType = entry.RecordType, StartedAtUtc = entry.StartedAtUtc, ElapsedMs = entry.Duration.TotalMilliseconds,
            Resolver = entry.ResolverHost, Port = entry.ResolverPort, RequestFormat = entry.RequestFormat, Transport = entry.UsedTransport,
            ServedFromCache = entry.ServedFromCache, Status = entry.Response?.Status, RetryReason = entry.RetryReason,
            Error = entry.Exception?.Message ?? (string.IsNullOrWhiteSpace(entry.Response?.Error) ? null : entry.Response!.Error)
        };
    }
}
