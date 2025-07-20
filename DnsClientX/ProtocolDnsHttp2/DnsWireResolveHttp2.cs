using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace DnsClientX {
    /// <summary>
    /// Extension helpers for resolving DNS queries using HTTP/2 transport.
    /// </summary>
    internal static class DnsWireResolveHttp2 {
        /// <summary>
        /// Performs a DNS-over-HTTP/2 query and deserializes the wire format response.
        /// </summary>
        /// <param name="client">HTTP client preconfigured for the DNS endpoint.</param>
        /// <param name="name">Domain name to query.</param>
        /// <param name="type">Record type to query.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="debug">Enable detailed logging.</param>
        /// <param name="endpointConfiguration">Endpoint configuration details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Parsed <see cref="DnsResponse"/> from the server.</returns>
        internal static async Task<DnsResponse> ResolveWireFormatHttp2(this HttpClient client, string name,
            DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug,
            Configuration endpointConfiguration, CancellationToken cancellationToken) {
            var dnsMessage = DnsWireQueryBuilder.BuildQuery(name, type, requestDnsSec, endpointConfiguration);
            var base64UrlDnsMessage = dnsMessage.ToBase64Url();
            string url = $"?dns={base64UrlDnsMessage}";

            using HttpRequestMessage req = new(HttpMethod.Get, url);
#if NET5_0_OR_GREATER
            req.Version = HttpVersion.Version20;
            req.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
#else
            req.Version = new Version(2, 0);
#endif
            if (debug) {
                Settings.Logger.WriteDebug("Query Name: " + name + " type: " + type + " url: " + req.RequestUri);
                Settings.Logger.WriteDebug("Query DnsWireFormatBytes: " + base64UrlDnsMessage);
            }

            try {
                using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
                byte[] responseBytes = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                DnsResponse response;
                if (res.StatusCode == HttpStatusCode.OK) {
                    response = await res.DeserializeDnsWireFormat(debug, responseBytes).ConfigureAwait(false);
                } else {
                    try {
                        response = await res.DeserializeDnsWireFormat(debug, responseBytes).ConfigureAwait(false);
                    } catch {
                        response = new DnsResponse {
                            Status = DnsResponseCode.ServerFailure,
                            Questions = [ new DnsQuestion { Name = name, Type = type, OriginalName = name } ]
                        };
                    }
                }
                response.AddServerDetails(endpointConfiguration);
                if (res.StatusCode != HttpStatusCode.OK || !string.IsNullOrEmpty(response.Error)) {
                    string body = string.Empty;
                    if (res.StatusCode != HttpStatusCode.OK) {
                        try {
                            body = Encoding.UTF8.GetString(responseBytes);
                        } catch {
                            body = string.Empty;
                        }
                    }

                    string message = string.Concat(
                        $"Failed to query type {type} of \"{name}\", received HTTP status code {res.StatusCode}.",
                        string.IsNullOrEmpty(response.Error) ? string.Empty : $"\nError: {response.Error}",
                        string.IsNullOrEmpty(body) ? string.Empty : $"\nBody: {body}",
                        response.Comments is null ? string.Empty : $"\nComments: {string.Join(", ", response.Comments)}");
                    throw new DnsClientException(message, response);
                }

                return response;
            } catch (HttpRequestException ex) {
                DnsResponseCode responseCode;
                if (ex.InnerException is TaskCanceledException || ex.InnerException is TimeoutException) {
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

                DnsResponse response = new DnsResponse {
                    Questions = [
                        new DnsQuestion {
                            Name = name,
                            RequestFormat = DnsRequestFormat.DnsOverHttp2,
                            HostName = client.BaseAddress?.Host ?? string.Empty,
                            Port = client.BaseAddress?.Port ?? 0,
                            Type = type,
                            OriginalName = name
                        }
                    ],
                    Status = responseCode
                };
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"Failed to query type {type} of \"{name}\" =>{ex.Message} {ex.InnerException?.Message}";
                return response;
            } catch (InvalidOperationException ex) {
                DnsResponse response = new DnsResponse {
                    Questions = [
                        new DnsQuestion {
                            Name = name,
                            RequestFormat = DnsRequestFormat.DnsOverHttp2,
                            HostName = client.BaseAddress?.Host ?? string.Empty,
                            Port = client.BaseAddress?.Port ?? 0,
                            Type = type,
                            OriginalName = name
                        }
                    ],
                    Status = DnsResponseCode.ServerFailure
                };
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"Failed to query type {type} of \"{name}\" =>{ex.Message}";
                return response;
            }
        }
    }
}
