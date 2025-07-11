using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class providing latency measurement helper.
    /// </summary>
    public partial class ClientX {
        /// <summary>
        /// Sends a simple DNS query and returns the round-trip time.
        /// </summary>
        /// <param name="name">Domain name to query. Defaults to <c>example.com</c>.</param>
        /// <param name="type">DNS record type to query. Defaults to <see cref="DnsRecordType.A"/>.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>Latency measured for the DNS query.</returns>
        public async Task<TimeSpan> MeasureLatencyAsync(string name = "example.com", DnsRecordType type = DnsRecordType.A, CancellationToken cancellationToken = default) {
            var sw = Stopwatch.StartNew();
            await Resolve(name, type, cancellationToken: cancellationToken).ConfigureAwait(false);
            sw.Stop();
            return sw.Elapsed;
        }

        /// <summary>
        /// Sends a simple DNS query and returns the round-trip time. Synchronous version.
        /// </summary>
        /// <param name="name">Domain name to query. Defaults to <c>example.com</c>.</param>
        /// <param name="type">DNS record type to query. Defaults to <see cref="DnsRecordType.A"/>.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>Latency measured for the DNS query.</returns>
        public TimeSpan MeasureLatency(string name = "example.com", DnsRecordType type = DnsRecordType.A, CancellationToken cancellationToken = default) {
            return MeasureLatencyAsync(name, type, cancellationToken).RunSync(cancellationToken);
        }
    }
}
