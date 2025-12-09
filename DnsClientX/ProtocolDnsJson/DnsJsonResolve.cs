using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Text.Json;

namespace DnsClientX {
    /// <summary>
    /// Extension methods for performing DNS queries using the DNS JSON API.
    /// </summary>
    internal static class DnsJsonResolve {
        /// <summary>
        /// Sends a DNS query in JSON format and returns the response.
        /// </summary>
        /// <param name="client">The HttpClient used to send the request.</param>
        /// <param name="name">The domain name to query.</param>
        /// <param name="type">The type of DNS record to query.</param>
        /// <param name="requestDnsSec">If set to <c>true</c>, the method will request DNSSEC data in the response.</param>
        /// <param name="validateDnsSec">If set to <c>true</c>, the method will validate DNSSEC data.</param>
        /// <param name="debug">If set to <c>true</c>, the method will include debugging information in the response.</param>
        /// <param name="configuration">Provide configuration so it can be added to Question for display purposes</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        /// <exception cref="DnsClientException">Thrown when the HTTP request fails or the server returns an error.</exception>
        internal static async Task<DnsResponse> ResolveJsonFormat(this HttpClient client, string name,
            DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration configuration, CancellationToken cancellationToken) {
            string url = string.Concat($"?name={name.UrlEncode()}",
                type == DnsRecordType.A ? "" : $"&type={type.ToString().UrlEncode()}",
                requestDnsSec == false ? "" : $"&do=1", validateDnsSec == false ? "" : $"&cd=1");

            using HttpRequestMessage req = new(HttpMethod.Get, url);
            try {
                using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);

                DnsResponse response = await res.DeserializeResponse(debug).ConfigureAwait(false);
                response.AddServerDetails(configuration);
                return response;
            } catch (Exception ex) {
                DnsResponseCode responseCode;
                string message;

                switch (ex) {
                    case HttpRequestException _ when ex.InnerException is WebException webEx && webEx.Status == WebExceptionStatus.ConnectFailure:
                        responseCode = DnsResponseCode.Refused;
                        message = $"Failed to send HTTP request for type {type} of '{name}'. Error: {ex.Message}";
                        break;
                    case JsonException _:
                        responseCode = DnsResponseCode.ServerFailure;
                        message = $"Failed to parse JSON response for type {type} of '{name}'. Error: {ex.Message}";
                        break;
                    default:
                        responseCode = DnsResponseCode.ServerFailure;
                        message = $"Unexpected exception while querying type {type} of '{name}'. Error: {ex.Message}";
                        break;
                }

                DnsResponse response = new DnsResponse {
                    Questions = new[] {
                        new DnsQuestion {
                            Name = name,
                            RequestFormat = DnsRequestFormat.DnsOverHttps,
                            HostName = client.BaseAddress?.Host ?? string.Empty,
                            Port = client.BaseAddress?.Port ?? 0,
                            Type = type,
                            OriginalName = name
                        }
                    },
                    Status = responseCode,
                    Error = message
                };
                response.AddServerDetails(configuration);
                return response;
            }
        }

        /// <summary>
        /// Sends a DNS query in JSON format using HTTP POST and returns the response.
        /// </summary>
        internal static async Task<DnsResponse> ResolveJsonFormatPost(this HttpClient client, string name,
            DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration configuration, CancellationToken cancellationToken) {
            var payload = new ResolveRequest { Name = name };
            if (type != DnsRecordType.A) {
                payload.Type = type.ToString();
            }
            if (requestDnsSec) {
                payload.Do = 1;
            }
            if (validateDnsSec) {
                payload.Cd = 1;
            }
            string json = DnsJson.Serialize(payload, DnsJsonContext.Default.ResolveRequest);

            using HttpRequestMessage req = new(HttpMethod.Post, string.Empty) {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            try {
                using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
                DnsResponse response = await res.DeserializeResponse(debug).ConfigureAwait(false);
                response.AddServerDetails(configuration);
                return response;
            } catch (Exception ex) {
                DnsResponseCode responseCode;
                string message;

                switch (ex) {
                    case HttpRequestException _ when ex.InnerException is WebException webEx && webEx.Status == WebExceptionStatus.ConnectFailure:
                        responseCode = DnsResponseCode.Refused;
                        message = $"Failed to send HTTP request for type {type} of '{name}'. Error: {ex.Message}";
                        break;
                    case JsonException _:
                        responseCode = DnsResponseCode.ServerFailure;
                        message = $"Failed to parse JSON response for type {type} of '{name}'. Error: {ex.Message}";
                        break;
                    default:
                        responseCode = DnsResponseCode.ServerFailure;
                        message = $"Unexpected exception while querying type {type} of '{name}'. Error: {ex.Message}";
                        break;
                }

                DnsResponse response = new DnsResponse {
                    Questions = new[] {
                        new DnsQuestion {
                            Name = name,
                            RequestFormat = DnsRequestFormat.DnsOverHttps,
                            HostName = client.BaseAddress?.Host ?? string.Empty,
                            Port = client.BaseAddress?.Port ?? 0,
                            Type = type,
                            OriginalName = name
                        }
                    },
                    Status = responseCode,
                    Error = message
                };
                response.AddServerDetails(configuration);
                return response;
            }
        }
    }
}
