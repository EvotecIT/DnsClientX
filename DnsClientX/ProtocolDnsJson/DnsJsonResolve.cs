using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;
using System;

namespace DnsClientX {
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
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        /// <exception cref="DnsClientException">Thrown when the HTTP request fails or the server returns an error.</exception>
        internal static async Task<DnsResponse> ResolveJsonFormat(this HttpClient client, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration configuration) {
            string url = string.Concat($"?name={name.UrlEncode()}", type == DnsRecordType.A ? "" : $"&type={type.ToString().UrlEncode()}", requestDnsSec == false ? "" : $"&do=1", validateDnsSec == false ? "" : $"&cd=1");

            using HttpRequestMessage req = new(HttpMethod.Get, url);
            try {
                using HttpResponseMessage res = await client.SendAsync(req);

                DnsResponse response = await res.Deserialize<DnsResponse>(debug);
                response.AddServerDetails(configuration);
                return response;
            } catch (HttpRequestException ex) {
                string message = $"Failed to send HTTP request for type {type} of '{name}'.";
                message += $" Error: {ex.Message}";
                if (ex.InnerException?.Message != null) {
                    message += $" Inner Exception: {ex.InnerException.Message}";
                }
                throw new DnsClientException(message);
            } catch (JsonException jsonEx) {
                throw new DnsClientException($"Failed to parse JSON response for type {type} of '{name}'. Error: {jsonEx.Message}");
            } catch (Exception ex) {
                throw new DnsClientException($"Unexpected exception while querying type {type} of '{name}'. Error: {ex.Message}");
            }
        }
    }
}
