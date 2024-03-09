using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class DnsWireResolvePost {
        /// <summary>
        /// Sends a DNS query in wire format using HTTP POST and returns the response.
        ///
        /// TODO: This method is not yet implemented.
        /// </summary>
        /// <param name="client">The HttpClient used to send the request.</param>
        /// <param name="name">The domain name to query.</param>
        /// <param name="type">The type of DNS record to query.</param>
        /// <param name="requestDnsSec">If set to <c>true</c>, the method will request DNSSEC data in the response.</param>
        /// <param name="validateDnsSec">If set to <c>true</c>, the method will validate DNSSEC data.</param>
        /// <param name="debug">If set to <c>true</c>, the method will include debugging information in the response.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the 'name' parameter is null or empty.</exception>
        /// <exception cref="DnsClientException">Thrown when the HTTP request fails or the server returns an error.</exception>
        internal static async Task<DnsResponse> ResolveWireFormatPost(this HttpClient client, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug = false) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var query = new DnsMessage(name, type, requestDnsSec);
            var queryBytes = query.SerializeDnsWireFormat();

            if (debug) {
                // Print the DNS wire format bytes to the console
                Console.WriteLine("Query    Name: " + name + " type: " + type);
                Console.WriteLine($"Sending query: {BitConverter.ToString(queryBytes)}");
            }

            var content = new ByteArrayContent(queryBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/dns-message");
            content.Headers.ContentLength = queryBytes.Length;

            //var response = await client.PostAsync(Configuration.BaseUri, content);
            var response = await client.PostAsync(client.BaseAddress, content);

            //if (response.IsSuccessStatusCode) {
            return await response.DeserializeDnsWireFormat(debug);
            //} else {
            //    throw new DnsOverHttpsException($"HTTP request failed with status code: {response.StatusCode}");
            //}
            //}
        }
    }
}
