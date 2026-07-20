using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace DnsClientX {
    /// <summary>
    /// Methods for performing DNS queries over various HTTP transports using raw wire format.
    /// </summary>
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
        /// <param name="useStandardDnsQueryPath">Use the standard <c>/dns-query</c> wire endpoint when the configured URI is a JSON resolver endpoint.</param>
        /// <returns>A Task that represents the asynchronous operation. The Task's result is a DnsResponse that contains the DNS response.</returns>
        /// <exception cref="DnsClientException">Thrown when the HTTP request fails or the server returns an error.</exception>
        internal static async Task<DnsResponse> ResolveWireFormatGet(this HttpClient client, string name,
            DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug,
            Configuration endpointConfiguration, CancellationToken cancellationToken, bool useStandardDnsQueryPath = false) {
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
                options = edns.GetEffectiveOptions();
            }
            var dnsMessage = new DnsMessage(name, type, new DnsMessageOptions(
                RequestDnsSec: requestDnsSec,
                EnableEdns: enableEdns,
                UdpBufferSize: udpSize,
                Subnet: string.IsNullOrEmpty(subnet) ? null : new EdnsClientSubnetOption(subnet!),
                CheckingDisabled: endpointConfiguration.CheckingDisabled || validateDnsSec,
                Options: options,
                RecursionDesired: endpointConfiguration.RecursionDesired));
            var base64UrlDnsMessage = dnsMessage.ToBase64Url();
            string url = $"?dns={base64UrlDnsMessage}";
            Uri? requestUri = null;
            if (useStandardDnsQueryPath) {
                Uri? baseUri = endpointConfiguration.BaseUri ?? client.BaseAddress;
                if (baseUri == null) throw new DnsClientException("A base URI is required for DNSSEC wire-format validation over HTTPS.");
                var builder = new UriBuilder(baseUri) { Path = "/dns-query", Query = "dns=" + base64UrlDnsMessage };
                requestUri = builder.Uri;
            }

            using HttpRequestMessage req = new(HttpMethod.Get, requestUri ?? new Uri(url, UriKind.Relative));
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/dns-message"));
#if NET5_0_OR_GREATER
            req.Version = endpointConfiguration.HttpVersion;
            req.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
#endif

            if (debug) {
                // Print the DNS wire format bytes to the logger
                Settings.Logger.WriteDebug("Query Name: " + name + " type: " + type + " url: " + req.RequestUri);
                Settings.Logger.WriteDebug("Query DnsWireFormatBytes: " + (base64UrlDnsMessage));
            }

            try {
                using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
                return await DeserializeDnsWireHttpResponse(
                    res, debug, dnsMessage, name, type, endpointConfiguration).ConfigureAwait(false);
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
                    if (error.IndexOf("ssl", StringComparison.Ordinal) >= 0 ||
                        error.IndexOf("certificate", StringComparison.Ordinal) >= 0 ||
                        error.IndexOf("handshake", StringComparison.Ordinal) >= 0) {
                        responseCode = DnsResponseCode.Refused;
                    } else if (error.IndexOf("timeout", StringComparison.Ordinal) >= 0) {
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

        /// <summary>
        /// Validates the HTTP envelope before treating the response body as an RFC 8484 DNS message.
        /// </summary>
        internal static async Task<DnsResponse> DeserializeDnsWireHttpResponse(
            HttpResponseMessage httpResponse,
            bool debug,
            DnsMessage query,
            string name,
            DnsRecordType type,
            Configuration endpointConfiguration) {
            byte[] responseBytes = await httpResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            DnsResponse failure = new() {
                Status = DnsResponseCode.ServerFailure,
                Questions = [new DnsQuestion {
                    Name = name,
                    OriginalName = name,
                    Type = type,
                    RequestFormat = endpointConfiguration.RequestFormat
                }]
            };
            failure.AddServerDetails(endpointConfiguration);

            if (!httpResponse.IsSuccessStatusCode) {
                string body = GetBodyPreview(responseBytes);
                string message = $"Failed to query type {type} of \"{name}\", received HTTP status code {httpResponse.StatusCode}." +
                                 (string.IsNullOrEmpty(body) ? string.Empty : $"\nBody: {body}");
                failure.Error = message;
                throw new DnsClientException(message, failure);
            }

            if (responseBytes.Length == 0) {
                string message = $"Failed to query type {type} of \"{name}\", received an empty response " +
                                 $"with HTTP status code {httpResponse.StatusCode}.";
                failure.Error = message;
                throw new DnsClientException(message, failure);
            }

            string? mediaType = httpResponse.Content.Headers.ContentType?.MediaType;
            if (!string.IsNullOrWhiteSpace(mediaType) &&
                !string.Equals(mediaType, "application/dns-message", StringComparison.OrdinalIgnoreCase)) {
                string message = $"Failed to query type {type} of \"{name}\": HTTP response media type " +
                                 $"'{mediaType}' is not application/dns-message.";
                failure.Error = message;
                throw new DnsClientException(message, failure);
            }

            DnsResponse response = await httpResponse
                .DeserializeDnsWireResponse(debug, responseBytes, query)
                .ConfigureAwait(false);
            response.AddServerDetails(endpointConfiguration);
            if (!string.IsNullOrEmpty(response.Error)) {
                string message = string.Concat(
                    $"Failed to query type {type} of \"{name}\".",
                    $"\nError: {response.Error}",
                    response.Comments is null ? string.Empty : $"\nComments: {string.Join(", ", response.Comments)}");
                throw new DnsClientException(message, response);
            }
            return response;
        }

        private static string GetBodyPreview(byte[] bytes) {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            const int maxPreviewBytes = 512;
            int count = Math.Min(maxPreviewBytes, bytes.Length);
            string preview = Encoding.UTF8.GetString(bytes, 0, count).Trim();
            return bytes.Length > maxPreviewBytes ? preview + "..." : preview;
        }
    }
}
