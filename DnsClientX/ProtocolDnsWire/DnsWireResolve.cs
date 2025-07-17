using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

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
            var edns = endpointConfiguration.EdnsOptions;
            bool enableEdns = endpointConfiguration.EnableEdns;
            int udpSize = endpointConfiguration.UdpBufferSize;
            string? subnet = endpointConfiguration.Subnet;
            System.Collections.Generic.IEnumerable<EdnsOption>? options = null;
            if (edns != null) {
                enableEdns = edns.EnableEdns;
                udpSize = edns.UdpBufferSize;
                subnet = edns.Subnet?.Subnet;
                options = edns.Options;
            }
            var dnsMessage = new DnsMessage(name, type, requestDnsSec, enableEdns, udpSize, subnet, endpointConfiguration.CheckingDisabled, endpointConfiguration.SigningKey, options);
            var base64UrlDnsMessage = dnsMessage.ToBase64Url();
            string url = $"?dns={base64UrlDnsMessage}";

            using HttpRequestMessage req = new(HttpMethod.Get, url);

            if (debug) {
                // Print the DNS wire format bytes to the logger
                Settings.Logger.WriteDebug("Query Name: " + name + " type: " + type + " url: " + req.RequestUri);
                Settings.Logger.WriteDebug("Query DnsWireFormatBytes: " + (base64UrlDnsMessage));
            }

            try {
                using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
                DnsResponse response = await res.DeserializeDnsWireFormat(debug).ConfigureAwait(false);
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
                // If the request fails, inspect details to determine the most appropriate DNS response code
                DnsResponseCode responseCode;
                if (ex.InnerException is TaskCanceledException || ex.InnerException is TimeoutException) {
                    // Timeouts are treated as server failures
                    responseCode = DnsResponseCode.ServerFailure;
                } else if (ex.InnerException is WebException webEx) {
                    switch (webEx.Status) {
                        case WebExceptionStatus.Timeout:
                            responseCode = DnsResponseCode.ServerFailure;
                            break;
                        case WebExceptionStatus.ConnectFailure:
                            responseCode = DnsResponseCode.Refused;
                            break;
                        case WebExceptionStatus.NameResolutionFailure:
                            responseCode = DnsResponseCode.ServerFailure;
                            break;
                        case WebExceptionStatus.TrustFailure:
                        case WebExceptionStatus.SecureChannelFailure:
                            responseCode = DnsResponseCode.Refused;
                            break;
                        default:
                            responseCode = DnsResponseCode.ServerFailure;
                            break;
                    }
                } else {
                    var error = (ex.InnerException?.Message ?? string.Empty).ToLowerInvariant();
                    if (error.Contains("ssl") || error.Contains("certificate") || error.Contains("handshake")) {
                        responseCode = DnsResponseCode.Refused;
                    } else if (error.Contains("timeout")) {
                        responseCode = DnsResponseCode.ServerFailure;
                    } else {
                        responseCode = DnsResponseCode.ServerFailure;
                    }
                }

                DnsResponse response = new DnsResponse();
                response.Questions = [
                    new DnsQuestion() {
                        Name = name,
                        RequestFormat = DnsRequestFormat.DnsOverHttps,
                        HostName = client.BaseAddress?.Host ?? string.Empty,
                        Port = client.BaseAddress?.Port ?? 0,
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
