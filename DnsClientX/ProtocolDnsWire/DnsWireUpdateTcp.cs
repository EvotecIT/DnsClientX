using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Sends DNS UPDATE messages using TCP transport.
    /// </summary>
    internal static class DnsWireUpdateTcp {
        /// <summary>
        /// Sends a DNS UPDATE message to add or modify a record.
        /// </summary>
        /// <param name="dnsServer">Server address.</param>
        /// <param name="port">Server port.</param>
        /// <param name="zone">Zone to update.</param>
        /// <param name="name">Record name.</param>
        /// <param name="type">Record type.</param>
        /// <param name="data">Record data.</param>
        /// <param name="ttl">Record TTL.</param>
        /// <param name="debug">Whether debugging is enabled.</param>
        /// <param name="endpointConfiguration">Endpoint settings.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Response from the DNS server.</returns>
        internal static async Task<DnsResponse> UpdateRecordAsync(string dnsServer, int port, string zone, string name, DnsRecordType type, string data, int ttl, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            DnsUpdateRequestMessage message = DnsUpdateMessage.CreateAddMessage(zone, name, type, data, ttl, endpointConfiguration.TsigKey);
            byte[] responseBuffer = await SendMessageOverTcp(message.WireData, dnsServer, port, endpointConfiguration, cancellationToken).ConfigureAwait(false);
            if (endpointConfiguration.TsigKey != null) DnsTsig.VerifyResponse(responseBuffer, message.TransactionId, endpointConfiguration.TsigKey, message.TsigMac);
            DnsResponse response = await DnsWire.DeserializeDnsUpdateResponse(responseBuffer, debug, message.TransactionId, message.Zone).ConfigureAwait(false);
            response.AddServerDetails(endpointConfiguration);
            return response;
        }

        /// <summary>
        /// Sends a DNS UPDATE message to delete a record.
        /// </summary>
        /// <param name="dnsServer">Server address.</param>
        /// <param name="port">Server port.</param>
        /// <param name="zone">Zone containing the record.</param>
        /// <param name="name">Record name.</param>
        /// <param name="type">Record type.</param>
        /// <param name="debug">Whether debugging is enabled.</param>
        /// <param name="endpointConfiguration">Endpoint settings.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Response from the DNS server.</returns>
        internal static async Task<DnsResponse> DeleteRecordAsync(string dnsServer, int port, string zone, string name, DnsRecordType type, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            DnsUpdateRequestMessage message = DnsUpdateMessage.CreateDeleteRrsetMessage(zone, name, type, endpointConfiguration.TsigKey);
            byte[] responseBuffer = await SendMessageOverTcp(message.WireData, dnsServer, port, endpointConfiguration, cancellationToken).ConfigureAwait(false);
            if (endpointConfiguration.TsigKey != null) DnsTsig.VerifyResponse(responseBuffer, message.TransactionId, endpointConfiguration.TsigKey, message.TsigMac);
            DnsResponse response = await DnsWire.DeserializeDnsUpdateResponse(responseBuffer, debug, message.TransactionId, message.Zone).ConfigureAwait(false);
            response.AddServerDetails(endpointConfiguration);
            return response;
        }

        internal static async Task<DnsResponse> DeleteRecordValueAsync(string dnsServer, int port, string zone,
            string name, DnsRecordType type, string data, bool debug, Configuration endpointConfiguration,
            CancellationToken cancellationToken) {
            DnsUpdateRequestMessage message = DnsUpdateMessage.CreateDeleteValueMessage(zone, name, type, data, endpointConfiguration.TsigKey);
            byte[] responseBuffer = await SendMessageOverTcp(message.WireData, dnsServer, port, endpointConfiguration, cancellationToken).ConfigureAwait(false);
            if (endpointConfiguration.TsigKey != null) DnsTsig.VerifyResponse(responseBuffer, message.TransactionId, endpointConfiguration.TsigKey, message.TsigMac);
            DnsResponse response = await DnsWire.DeserializeDnsUpdateResponse(responseBuffer, debug, message.TransactionId, message.Zone).ConfigureAwait(false);
            response.AddServerDetails(endpointConfiguration);
            return response;
        }

        private static async Task<byte[]> SendMessageOverTcp(
            byte[] message,
            string dnsServer,
            int port,
            Configuration endpointConfiguration,
            CancellationToken cancellationToken) {
            (System.Net.IPAddress? address, string? error) = await DnsServerResolver.ResolveAsync(
                dnsServer,
                endpointConfiguration.TimeOut,
                cancellationToken,
                endpointConfiguration.DnsServerResolutionSuccessTtl,
                endpointConfiguration.DnsServerResolutionFailureTtl,
                endpointConfiguration.DnsServerResolutionAllowStale,
                endpointConfiguration.DnsServerResolutionStaleTtl,
                endpointConfiguration.DnsServerResolutionFailureBackoffEnabled,
                endpointConfiguration.DnsServerResolutionFailureBackoffFactor,
                endpointConfiguration.DnsServerResolutionFailureBackoffMaxTtl,
                endpointConfiguration.PreferredAddressFamily).ConfigureAwait(false);
            if (address == null) throw new DnsClientException(error ?? $"DNS server '{dnsServer}' could not be resolved.");
            return await DnsWireResolveTcp.SendQueryOverTcp(
                message,
                address.ToString(),
                port,
                endpointConfiguration.TimeOut,
                cancellationToken,
                endpointConfiguration.LocalEndPoint).ConfigureAwait(false);
        }
    }
}
