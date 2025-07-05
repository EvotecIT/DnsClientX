using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class DnsWireResolveHttp3 {
        internal static async Task<DnsResponse> ResolveWireFormatHttp3(this HttpClient client, string name,
            DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug,
            Configuration endpointConfiguration, CancellationToken cancellationToken) {
            var dnsMessage = new DnsMessage(name, type, requestDnsSec, endpointConfiguration.EnableEdns, endpointConfiguration.UdpBufferSize, endpointConfiguration.Subnet);
            var base64UrlDnsMessage = dnsMessage.ToBase64Url();
            string url = $"?dns={base64UrlDnsMessage}";

            using HttpRequestMessage req = new(HttpMethod.Get, url);
#if NET8_0_OR_GREATER
            req.Version = HttpVersion.Version30;
#else
            req.Version = new Version(3, 0);
#endif
            if (debug) {
                Settings.Logger.WriteDebug("Query Name: " + name + " type: " + type + " url: " + req.RequestUri);
                Settings.Logger.WriteDebug("Query DnsWireFormatBytes: " + base64UrlDnsMessage);
            }

            try {
                using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
                DnsResponse response = await res.DeserializeDnsWireFormat(debug).ConfigureAwait(false);
                response.AddServerDetails(endpointConfiguration);
                if (res.StatusCode != HttpStatusCode.OK || !string.IsNullOrEmpty(response.Error)) {
                    string message = string.Concat(
                        $"Failed to query type {type} of \"{name}\", received HTTP status code {res.StatusCode}.",
                        string.IsNullOrEmpty(response.Error) ? string.Empty : $"\nError: {response.Error}",
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
                            RequestFormat = DnsRequestFormat.DnsOverHttp3,
                            HostName = client.BaseAddress.Host,
                            Port = client.BaseAddress.Port,
                            Type = type,
                            OriginalName = name
                        }
                    ],
                    Status = responseCode
                };
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"Failed to query type {type} of \"{name}\" =>{ex.Message} {ex.InnerException?.Message}";
                return response;
            }
        }
    }
}
