using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace DnsClientX {
    /// <summary>
    /// Extension helpers for performing DNS queries over HTTP/3.
    /// </summary>
    internal static class DnsWireResolveHttp3 {
        /// <summary>
        /// Executes a DNS query using HTTP/3 transport and deserializes the result.
        /// </summary>
        /// <param name="client">HTTP client configured for the DNS endpoint.</param>
        /// <param name="name">Domain name to query.</param>
        /// <param name="type">Record type to query.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="debug">Enable detailed logging.</param>
        /// <param name="endpointConfiguration">Endpoint configuration details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Parsed <see cref="DnsResponse"/> instance.</returns>
        internal static async Task<DnsResponse> ResolveWireFormatHttp3(this HttpClient client, string name,
            DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug,
            Configuration endpointConfiguration, CancellationToken cancellationToken) {
            var dnsMessage = DnsWireQueryBuilder.BuildQuery(name, type, requestDnsSec, endpointConfiguration,
                checkingDisabled: endpointConfiguration.CheckingDisabled || validateDnsSec);
            var base64UrlDnsMessage = dnsMessage.ToBase64Url();
            string url = $"?dns={base64UrlDnsMessage}";

            using HttpRequestMessage req = new(HttpMethod.Get, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/dns-message"));
#if NET5_0_OR_GREATER
            req.Version = HttpVersion.Version30;
            req.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
#else
            req.Version = new Version(3, 0);
#endif
            if (debug) {
                Settings.Logger.WriteDebug("Query Name: " + name + " type: " + type + " url: " + req.RequestUri);
                Settings.Logger.WriteDebug("Query DnsWireFormatBytes: " + base64UrlDnsMessage);
            }

            try {
                using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
                return await DnsWireResolve.DeserializeDnsWireHttpResponse(
                    res, debug, dnsMessage, name, type, endpointConfiguration).ConfigureAwait(false);
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

                DnsResponse response = new DnsResponse {
                    Questions = [
                        new DnsQuestion {
                            Name = name,
                            RequestFormat = DnsRequestFormat.DnsOverHttp3,
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
                            RequestFormat = DnsRequestFormat.DnsOverHttp3,
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
