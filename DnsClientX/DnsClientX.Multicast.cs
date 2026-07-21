using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        /// <summary>
        /// Sends one multicast DNS query and returns every distinct response received during the configured timeout window.
        /// </summary>
        /// <param name="name">The multicast DNS name to query.</param>
        /// <param name="type">The requested record type.</param>
        /// <param name="cancellationToken">Token used to cancel the query.</param>
        /// <returns>Distinct responder messages; an empty array means no responder answered before the deadline.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not configured for multicast DNS.</exception>
        public Task<DnsResponse[]> ResolveMulticastAllAsync(
            string name,
            DnsRecordType type = DnsRecordType.A,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            Configuration configuration = EndpointConfiguration.CreateQuerySnapshot();
            if (configuration.RequestFormat != DnsRequestFormat.Multicast) {
                throw new InvalidOperationException("ResolveMulticastAllAsync requires a client configured with DnsRequestFormat.Multicast.");
            }

            return DnsWireResolveMulticast.ResolveWireFormatMulticastAll(
                configuration.Hostname!,
                configuration.Port,
                name,
                type,
                Debug,
                configuration,
                cancellationToken);
        }
    }
}
