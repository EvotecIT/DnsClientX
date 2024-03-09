using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class DnsWireResolvePost {
        /// <summary>
        /// Resolves the wire format post.
        /// TODO - This method is not yet working correctly
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="requestDnsSec">if set to <c>true</c> [request DNS sec].</param>
        /// <param name="validateDnsSec">if set to <c>true</c> [validate DNS sec].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">name - Name is null or empty.</exception>
        /// <exception cref="DnsClientException">HTTP request failed with status code: {response.StatusCode}</exception>
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
