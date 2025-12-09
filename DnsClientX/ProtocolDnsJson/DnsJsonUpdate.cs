using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Helper methods for performing DNS UPDATE operations using the JSON API.
    /// </summary>
    internal static class DnsJsonUpdate {
        internal static async Task<DnsResponse> UpdateJsonFormatPost(this HttpClient client, string zone, string name,
            DnsRecordType type, string data, int ttl, bool debug, Configuration configuration, CancellationToken cancellationToken) {
            var payload = new UpdateRequest {
                Zone = zone,
                Name = name,
                Type = type.ToString(),
                Data = data,
                Ttl = ttl
            };
            string json = DnsJson.Serialize(payload, DnsJsonContext.Default.UpdateRequest);
            using HttpRequestMessage req = new(HttpMethod.Post, string.Empty) { Content = new StringContent(json) };
            req.Content!.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            DnsResponse response = await res.DeserializeResponse(debug).ConfigureAwait(false);
            response.AddServerDetails(configuration);
            return response;
        }

        internal static async Task<DnsResponse> DeleteJsonFormatPost(this HttpClient client, string zone, string name,
            DnsRecordType type, bool debug, Configuration configuration, CancellationToken cancellationToken) {
            var payload = new UpdateRequest {
                Zone = zone,
                Name = name,
                Type = type.ToString(),
                Delete = true
            };
            string json = DnsJson.Serialize(payload, DnsJsonContext.Default.UpdateRequest);
            using HttpRequestMessage req = new(HttpMethod.Post, string.Empty) { Content = new StringContent(json) };
            req.Content!.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using HttpResponseMessage res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            DnsResponse response = await res.DeserializeResponse(debug).ConfigureAwait(false);
            response.AddServerDetails(configuration);
            return response;
        }
    }
}
