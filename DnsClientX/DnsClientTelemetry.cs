using System;
using System.Diagnostics;
#if NET8_0_OR_GREATER
using System.Diagnostics.Metrics;
#endif

namespace DnsClientX {
    /// <summary>
    /// Exposes dependency-free query telemetry. Modern targets also emit <c>ActivitySource</c>
    /// activities and <c>Meter</c> measurements for OpenTelemetry-compatible listeners.
    /// </summary>
    public static class DnsClientTelemetry {
        /// <summary>Activity source name used on modern .NET targets.</summary>
        public const string ActivitySourceName = "DnsClientX";
        /// <summary>Meter name used on modern .NET targets.</summary>
        public const string MeterName = "DnsClientX";

#if NET8_0_OR_GREATER
        private static readonly ActivitySource QueryActivitySource = new(ActivitySourceName);
        private static readonly Meter QueryMeter = new(MeterName);
        private static readonly Counter<long> QueryCounter = QueryMeter.CreateCounter<long>("dnsclientx.query.count", description: "Completed DNS queries.");
        private static readonly Counter<long> FailureCounter = QueryMeter.CreateCounter<long>("dnsclientx.query.failure.count", description: "DNS queries that completed with an error or exception.");
        private static readonly Histogram<double> DurationHistogram = QueryMeter.CreateHistogram<double>("dnsclientx.query.duration", "ms", "End-to-end DNS query duration.");

        /// <summary>Gets the activity source used for DNS query tracing.</summary>
        public static ActivitySource ActivitySource => QueryActivitySource;
        /// <summary>Gets the meter used for DNS query metrics.</summary>
        public static Meter Meter => QueryMeter;
#endif

        /// <summary>
        /// Occurs after a public query finishes. Subscribe to bridge telemetry on targets that do not expose ActivitySource.
        /// </summary>
        public static event EventHandler<DnsQueryTelemetryEventArgs>? QueryCompleted;

        internal static DnsQueryTelemetryScope? StartQuery(string name, DnsRecordType type, Configuration configuration) {
#if NET8_0_OR_GREATER
            if (QueryCompleted == null
                && !QueryActivitySource.HasListeners()
                && !QueryCounter.Enabled
                && !FailureCounter.Enabled
                && !DurationHistogram.Enabled) {
                return null;
            }
#else
            if (QueryCompleted == null) return null;
#endif
            return new DnsQueryTelemetryScope(name, type, configuration);
        }

        internal static void Publish(DnsQueryTelemetryEventArgs args) {
#if NET8_0_OR_GREATER
            TagList tags = default;
            tags.Add("dns.question.type", args.Type.ToString());
            tags.Add("dns.transport", args.UsedTransport?.ToString() ?? args.RequestFormat.ToString());
            tags.Add("dns.response.code", args.Status?.ToString() ?? "exception");
            QueryCounter.Add(1, tags);
            DurationHistogram.Record(args.Duration.TotalMilliseconds, tags);
            if (!args.Succeeded) {
                FailureCounter.Add(1, tags);
            }
#endif
            EventHandler<DnsQueryTelemetryEventArgs>? handlers = QueryCompleted;
            if (handlers == null) return;
            foreach (EventHandler<DnsQueryTelemetryEventArgs> handler
                     in handlers.GetInvocationList()) {
                try {
                    handler(null, args);
                } catch (Exception) {
                    // Observability adapters must never change DNS query semantics.
                }
            }
        }

        internal sealed class DnsQueryTelemetryScope : IDisposable {
            private readonly string name;
            private readonly DnsRecordType type;
            private readonly DnsRequestFormat requestFormat;
            private readonly string? configuredServer;
            private readonly Stopwatch stopwatch = Stopwatch.StartNew();
#if NET8_0_OR_GREATER
            private readonly Activity? activity;
#endif
            private bool completed;

            internal DnsQueryTelemetryScope(string name, DnsRecordType type, Configuration configuration) {
                this.name = name;
                this.type = type;
                requestFormat = configuration.RequestFormat;
                configuredServer = configuration.Hostname ?? configuration.BaseUri?.Host;
#if NET8_0_OR_GREATER
                activity = QueryActivitySource.StartActivity("DnsClientX.Resolve", ActivityKind.Client);
                activity?.SetTag("dns.question.name", name);
                activity?.SetTag("dns.question.type", type.ToString());
                activity?.SetTag("dns.transport", requestFormat.ToString());
                activity?.SetTag("server.address", configuredServer);
#endif
            }

            internal void Complete(DnsResponse response) {
                if (completed) return;
                completed = true;
                stopwatch.Stop();
                string? actualServer = response.ServerAddress ?? configuredServer;
                bool succeeded = response.Status == DnsResponseCode.NoError
                    && string.IsNullOrEmpty(response.Error)
                    && response.DnsSecValidationStatus != DnsSecValidationStatus.Bogus
                    && response.DnsSecValidationStatus != DnsSecValidationStatus.Indeterminate;
#if NET8_0_OR_GREATER
                activity?.SetTag("server.address", actualServer);
                activity?.SetTag("network.transport", response.UsedTransport.ToString());
                activity?.SetTag("dns.response.code", response.Status.ToString());
                if (!succeeded) {
                    activity?.SetStatus(ActivityStatusCode.Error, response.Error ?? response.Status.ToString());
                }
#endif
                Publish(new DnsQueryTelemetryEventArgs(name, type, requestFormat, response.UsedTransport,
                    actualServer, stopwatch.Elapsed, response.Status, succeeded, null));
            }

            internal void Fail(Exception exception) {
                if (completed) return;
                completed = true;
                stopwatch.Stop();
#if NET8_0_OR_GREATER
                activity?.SetTag("error.type", exception.GetType().FullName);
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
#endif
                Publish(new DnsQueryTelemetryEventArgs(name, type, requestFormat, null,
                    configuredServer, stopwatch.Elapsed, null, succeeded: false, exception));
            }

            /// <inheritdoc />
            public void Dispose() {
#if NET8_0_OR_GREATER
                activity?.Dispose();
#endif
            }
        }
    }
}
