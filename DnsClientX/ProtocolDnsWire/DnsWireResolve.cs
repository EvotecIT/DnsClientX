using System.Net.Http;
using System.Net;
using System.Threading;
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
        /// <param name="endpointConfiguration">Configuration used for server details.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A Task that represents the asynchronous operation. The Task's result is a DnsResponse that contains the DNS response.</returns>
        /// <exception cref="DnsClientException">Thrown when the HTTP request fails or the server returns an error.</exception>
        internal static async Task<DnsResponse> ResolveWireFormatGet(this HttpClient client, string name,
            DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug,
            Configuration endpointConfiguration, CancellationToken cancellationToken) {
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
                using HttpResponseMessage res = await client.SendAsync(req, cancellationToken);
                DnsResponse response = await res.DeserializeDnsWireFormat(debug);
                response.AddServerDetails(endpointConfiguration);
                if (res.StatusCode != HttpStatusCode.OK || !string.IsNullOrEmpty(response.Error)) {
                    string message = string.Concat(
                        $"Failed to query type {type} of \"{name}\", received HTTP status code {res.StatusCode}.",
                        string.IsNullOrEmpty(response.Error) ? "" : $"\nError: {response.Error}",
                        response.Comments is null ? "" : $"\nComments: {string.Join(", ", response.Comments)}");

                    throw new DnsClientException(message, response);
                }

                return response;
            } catch (HttpRequestException ex) {
                // Determine response code based on the underlying reason of the HttpRequestException
                DnsResponseCode responseCode = DnsResponseCode.ServerFailure;

                if (ex.InnerException is WebException webEx) {
                    switch (webEx.Status) {
                        case WebExceptionStatus.ConnectFailure:
                            responseCode = DnsResponseCode.Refused;
                            break;
                        case WebExceptionStatus.Timeout:
                            responseCode = DnsResponseCode.ServerFailure;
                            break;
                        case WebExceptionStatus.NameResolutionFailure:
                            responseCode = DnsResponseCode.NXDomain;
                            break;
                        case WebExceptionStatus.TrustFailure:
                        case WebExceptionStatus.SecureChannelFailure:
                            responseCode = DnsResponseCode.Refused;
                            break;
                        default:
                            responseCode = DnsResponseCode.ServerFailure;
                            break;
                    }
                } else if (ex.InnerException is TimeoutException || ex.InnerException is TaskCanceledException) {
                    responseCode = DnsResponseCode.ServerFailure;
                } else if (ex.InnerException is System.Security.Authentication.AuthenticationException) {
                    responseCode = DnsResponseCode.Refused;
                }

                DnsResponse response = new DnsResponse();
                response.Questions = [
                    new DnsQuestion() {
                        Name = name,
                        RequestFormat = DnsRequestFormat.DnsOverHttps,
                        HostName = client.BaseAddress.Host,
                        Port = client.BaseAddress.Port,
                        Type = type,
                        OriginalName = name
                    }
                ];
                response.Status = responseCode;
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"Failed to query type {type} of \"{name}\" => {ex.Message + " " + ex.InnerException?.Message}";
                return response;
            }
        }
    }
}
