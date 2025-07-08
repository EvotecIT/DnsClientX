using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace DnsClientX {
    internal static class DnsJsonUpdate {
        internal static async Task<DnsResponse> UpdateJsonFormatPost(this HttpClient client, string zone, string name,
            DnsRecordType type, string data, int ttl, bool debug, Configuration configuration, CancellationToken cancellationToken) {
            var payload = new Dictionary<string, object?> {
                ["zone"] = zone,
                ["name"] = name,
                ["type"] = type.ToString(),
                ["data"] = data,
                ["ttl"] = ttl
            };
            string json = JsonSerializer.Serialize(payload, DnsJson.JsonOptions);
            using HttpRequestMessage req = new(HttpMethod.Post, string.Empty) { Content = new StringContent(json) };
            req.Content!.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            DnsResponse response = await res.Deserialize<DnsResponse>(debug).ConfigureAwait(false);
            response.AddServerDetails(configuration);
            return response;
        }

        internal static async Task<DnsResponse> DeleteJsonFormatPost(this HttpClient client, string zone, string name,
            DnsRecordType type, bool debug, Configuration configuration, CancellationToken cancellationToken) {
            var payload = new Dictionary<string, object?> {
                ["zone"] = zone,
                ["name"] = name,
                ["type"] = type.ToString(),
                ["delete"] = true
            };
            string json = JsonSerializer.Serialize(payload, DnsJson.JsonOptions);
            using HttpRequestMessage req = new(HttpMethod.Post, string.Empty) { Content = new StringContent(json) };
            req.Content!.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            DnsResponse response = await res.Deserialize<DnsResponse>(debug).ConfigureAwait(false);
            response.AddServerDetails(configuration);
            return response;
        }
    }
}
