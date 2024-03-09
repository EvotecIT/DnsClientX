using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System;

namespace DnsClientX {
    internal static class DnsWireResolve {
        /// <summary>
        /// Sends a DNS query in wire format over HTTP GET and returns the response.
        /// </summary>
        /// <param name="client">The HttpClient used to send the request.</param>
        /// <param name="name">The domain name to query.</param>
        /// <param name="type">The type of DNS record to query.</param>
        /// <param name="requestDnsSec">If set to <c>true</c>, the query will request DNSSEC records.</param>
        /// <param name="validateDnsSec">If set to <c>true</c>, the response will be validated using DNSSEC.</param>
        /// <param name="debug">If set to <c>true</c>, debug information will be printed to the console.</param>
        /// <returns>A Task that represents the asynchronous operation. The Task's result is a DnsResponse that contains the DNS response.</returns>
        /// <exception cref="DnsClientException">Thrown when the HTTP request fails or the server returns an error.</exception>
        internal static async Task<DnsResponse> ResolveWireFormatGet(this HttpClient client, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug) {
            // For OpenDNS, we need to create a DNS message and base64url encode it
            var dnsMessage = new DnsMessage(name, type, requestDnsSec);
            var base64UrlDnsMessage = dnsMessage.ToBase64Url();
            string url = $"?dns={base64UrlDnsMessage}";

            using HttpRequestMessage req = new(HttpMethod.Get, url);

            if (debug) {
                // Print the DNS wire format bytes to the console
                Console.WriteLine("Query Name: " + name + " type: " + type + " url: " + req.RequestUri);
                Console.WriteLine("Query DnsWireFormatBytes: " + (base64UrlDnsMessage));
            }

            try {
                using HttpResponseMessage res = await client.SendAsync(req);
                DnsResponse response = await res.DeserializeDnsWireFormat(debug);

                if (res.StatusCode != HttpStatusCode.OK || !string.IsNullOrEmpty(response.Error)) {
                    string message = string.Concat(
                        $"Failed to query type {type} of \"{name}\", received HTTP status code {res.StatusCode}.",
                        string.IsNullOrEmpty(response.Error) ? "" : $"\nError: {response.Error}",
                        response.Comments is null ? "" : $"\nComments: {string.Join(", ", response.Comments)}");

                    throw new DnsClientException(message, response);
                }

                return response;
            } catch (HttpRequestException ex) {
                throw new DnsClientException($"Failed to query type {type} of \"{name}\" => {ex.Message + " " + ex.InnerException.Message}");
            }
        }
    }
}
