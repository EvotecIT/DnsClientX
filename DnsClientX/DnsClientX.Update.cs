using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class providing DNS UPDATE operations.
    /// </summary>
    /// <remarks>
    /// DNS UPDATE is described in <see href="https://www.rfc-editor.org/rfc/rfc2136">RFC 2136</see> and allows dynamic modification of zone data.
    /// </remarks>
    public partial class ClientX {
        /// <summary>
        /// Sends a DNS UPDATE request to add or modify a record in a zone.
        /// </summary>
        /// <param name="zone">Zone to update.</param>
        /// <param name="name">Record name.</param>
        /// <param name="type">Type of record.</param>
        /// <param name="data">Record data.</param>
        /// <param name="ttl">Time to live for the record.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>DNS response returned by the server.</returns>
        /// <exception cref="DnsClientException">Thrown when the server returns an error.</exception>
        public async Task<DnsResponse> UpdateRecordAsync(string zone, string name, DnsRecordType type, string data, int ttl = 300, CancellationToken cancellationToken = default) {
            if (string.IsNullOrEmpty(zone)) throw new ArgumentNullException(nameof(zone));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (ttl <= 0) throw new ArgumentOutOfRangeException(nameof(ttl));
            EndpointConfiguration.SelectHostNameStrategy();
            DnsResponse response;
            if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSONPOST) {
                response = await Client.UpdateJsonFormatPost(zone, name, type, data, ttl, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
            } else {
                response = await DnsWireUpdateTcp.UpdateRecordAsync(EndpointConfiguration.Hostname, EndpointConfiguration.Port, zone, name, type, data, ttl, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
            }
            if (response.Status != DnsResponseCode.NoError) {
                throw new DnsClientException($"DNS update failed with {response.Status}", response);
            }
            return response;
        }

        /// <summary>
        /// Sends a DNS UPDATE request to delete a record from a zone.
        /// </summary>
        /// <param name="zone">Zone containing the record.</param>
        /// <param name="name">Record name.</param>
        /// <param name="type">Type of record.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>DNS response returned by the server.</returns>
        /// <exception cref="DnsClientException">Thrown when the server returns an error.</exception>
        public async Task<DnsResponse> DeleteRecordAsync(string zone, string name, DnsRecordType type, CancellationToken cancellationToken = default) {
            if (string.IsNullOrEmpty(zone)) throw new ArgumentNullException(nameof(zone));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            EndpointConfiguration.SelectHostNameStrategy();
            DnsResponse response;
            if (EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSONPOST) {
                response = await Client.DeleteJsonFormatPost(zone, name, type, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
            } else {
                response = await DnsWireUpdateTcp.DeleteRecordAsync(EndpointConfiguration.Hostname, EndpointConfiguration.Port, zone, name, type, Debug, EndpointConfiguration, cancellationToken).ConfigureAwait(false);
            }
            if (response.Status != DnsResponseCode.NoError) {
                throw new DnsClientException($"DNS update failed with {response.Status}", response);
            }
            return response;
        }
    }
}
