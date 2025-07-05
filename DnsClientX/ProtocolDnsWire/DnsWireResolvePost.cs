using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class DnsWireResolvePost {
        /// <summary>
        /// Sends a DNS query in wire format using HTTP POST and returns the response.
        /// </summary>
        /// <param name="client">The HttpClient used to send the request.</param>
        /// <param name="name">The domain name to query.</param>
        /// <param name="type">The type of DNS record to query.</param>
        /// <param name="requestDnsSec">If set to <c>true</c>, the method will request DNSSEC data in the response.</param>
        /// <param name="validateDnsSec">If set to <c>true</c>, the method will validate DNSSEC data.</param>
        /// <param name="debug">If set to <c>true</c>, the method will include debugging information in the response.</param>
        /// <param name="endpointConfiguration">Provide configuration so it can be added to Question for display purposes</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the 'name' parameter is null or empty.</exception>
        /// <exception cref="DnsClientException">Thrown when the HTTP request fails or the server returns an error.</exception>
        internal static async Task<DnsResponse> ResolveWireFormatPost(this HttpClient client, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var edns = endpointConfiguration.EdnsOptions;
            bool enableEdns = edns?.EnableEdns ?? endpointConfiguration.EnableEdns;
            int udpSize = edns?.UdpBufferSize ?? endpointConfiguration.UdpBufferSize;
            string? subnet = edns?.Subnet ?? endpointConfiguration.Subnet;
            var query = new DnsMessage(name, type, requestDnsSec, enableEdns, udpSize, subnet);
            var queryBytes = query.SerializeDnsWireFormat();

            if (debug) {
                // Print the DNS wire format bytes to the logger
                Settings.Logger.WriteDebug("Query    Name: " + name + " type: " + type);
                Settings.Logger.WriteDebug($"Sending query: {BitConverter.ToString(queryBytes)}");
            }

            using ByteArrayContent content = new(queryBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/dns-message");

            using HttpResponseMessage postAsync = await client.PostAsync(client.BaseAddress, content, cancellationToken).ConfigureAwait(false);
            var response = await postAsync.DeserializeDnsWireFormat(debug).ConfigureAwait(false);
            response.AddServerDetails(endpointConfiguration);
            return response;
        }
    }
}
