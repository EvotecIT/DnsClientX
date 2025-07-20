using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
#if NET8_0_OR_GREATER
    /// <summary>
    /// Helper methods for resolving DNS queries over gRPC transport.
    /// </summary>
    internal static class DnsWireResolveGrpc {
        /// <summary>Factory for creating HTTP clients used for gRPC calls. Can be overridden in tests.</summary>
        internal static Func<Uri, HttpClient> ClientFactory { get; set; } = uri => new HttpClient {
            BaseAddress = uri,
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };
        /// <summary>Delegate used to send the HTTP request. Can be overridden in tests.</summary>
        internal static Func<HttpClient, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> SendAsync { get; set; } =
            (client, request, token) => client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

        /// <summary>
        /// Executes a DNS-over-gRPC query and deserializes the response.
        /// </summary>
        internal static async Task<DnsResponse> ResolveWireFormatGrpc(string dnsServer, int port, string name, DnsRecordType type,
            bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var query = DnsWireQueryBuilder.BuildQuery(name, type, requestDnsSec, endpointConfiguration);
            var queryBytes = query.SerializeDnsWireFormat();

            if (debug) {
                Settings.Logger.WriteDebug($"Query Name: {name} type: {type}");
                Settings.Logger.WriteDebug($"Sending query: {BitConverter.ToString(queryBytes)}");
            }

            Uri uri = new($"https://{dnsServer}:{port}");
            try {
                using var client = ClientFactory(uri);
                using var request = new HttpRequestMessage(HttpMethod.Post, "/DnsResolver/QueryDns") {
                    Version = HttpVersion.Version20,
                    VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
                    Content = new ByteArrayContent(CreateGrpcPayload(queryBytes))
                };
                request.Headers.Add("TE", "trailers");
                request.Content!.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc");
                using var responseMsg = await SendAsync(client, request, cancellationToken).ConfigureAwait(false);
                byte[] responseBytes = await responseMsg.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                var payload = ParseGrpcPayload(responseBytes);
                var response = await DnsWire.DeserializeDnsWireFormat(null, debug, payload).ConfigureAwait(false);
                response.AddServerDetails(endpointConfiguration);
                return response;
            } catch (Exception ex) {
                var response = new DnsResponse {
                    Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverGrpc, Type = type, OriginalName = name } ],
                    Status = DnsResponseCode.ServerFailure
                };
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"Failed to query type {type} of \"{name}\" => {ex.Message}";
                return response;
            }
        }

        private static byte[] CreateGrpcPayload(byte[] data) {
            var payload = new byte[data.Length + 5];
            payload[0] = 0; // uncompressed
            payload[1] = (byte)((data.Length >> 24) & 0xFF);
            payload[2] = (byte)((data.Length >> 16) & 0xFF);
            payload[3] = (byte)((data.Length >> 8) & 0xFF);
            payload[4] = (byte)(data.Length & 0xFF);
            Buffer.BlockCopy(data, 0, payload, 5, data.Length);
            return payload;
        }

        private static byte[] ParseGrpcPayload(byte[] responseBytes) {
            if (responseBytes.Length < 5) {
                return Array.Empty<byte>();
            }
            int len = (responseBytes[1] << 24) | (responseBytes[2] << 16) | (responseBytes[3] << 8) | responseBytes[4];
            if (len <= 0 || responseBytes.Length - 5 < len) {
                len = responseBytes.Length - 5;
            }
            var payload = new byte[len];
            Buffer.BlockCopy(responseBytes, 5, payload, 0, len);
            return payload;
        }
    }
#endif
}
