using System;
using System.Net;
using System.Net.Http;
using System.Globalization;
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

            var query = DnsWireQueryBuilder.BuildQuery(name, type, requestDnsSec, endpointConfiguration,
                checkingDisabled: endpointConfiguration.CheckingDisabled || validateDnsSec);
            var queryBytes = query.SerializeDnsWireFormat();

            if (debug) {
                Settings.Logger.WriteDebug($"Query Name: {name} type: {type}");
                Settings.Logger.WriteDebug($"Sending query: {BitConverter.ToString(queryBytes)}");
            }

            Uri uri = new($"https://{dnsServer}:{port}");
            try {
                using var timeoutCts = CreateTimeoutTokenSource(endpointConfiguration.TimeOut, cancellationToken);
                using var client = ClientFactory(uri);
                using var request = new HttpRequestMessage(HttpMethod.Post, "/DnsResolver/QueryDns") {
                    Version = HttpVersion.Version20,
                    VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
                    Content = new ByteArrayContent(CreateGrpcPayload(queryBytes))
                };
                request.Headers.Add("TE", "trailers");
                request.Content!.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc");
                using var responseMsg = await SendAsync(client, request, timeoutCts.Token).ConfigureAwait(false);
                if (responseMsg.StatusCode != HttpStatusCode.OK) {
                    throw new DnsClientException($"gRPC endpoint returned HTTP {(int)responseMsg.StatusCode} ({responseMsg.ReasonPhrase}).");
                }
                if (responseMsg.Content == null) {
                    throw new DnsClientException("gRPC endpoint returned no response content.");
                }
                byte[] responseBytes = await DnsHttpResponseBody.ReadBoundedAsync(
                    responseMsg.Content, DnsHttpResponseBody.MaxGrpcBytes, timeoutCts.Token).ConfigureAwait(false);
                EnsureGrpcSuccess(responseMsg);
                string? mediaType = responseMsg.Content.Headers.ContentType?.MediaType;
                if (mediaType == null ||
                    !(mediaType.Equals("application/grpc", StringComparison.OrdinalIgnoreCase) ||
                      mediaType.StartsWith("application/grpc+", StringComparison.OrdinalIgnoreCase))) {
                    throw new DnsClientException("gRPC endpoint returned a non-gRPC content type.");
                }
                var payload = ParseGrpcPayload(responseBytes);
                var response = await DnsWire.DeserializeDnsWireResponse(null, debug, payload, query).ConfigureAwait(false);
                response.AddServerDetails(endpointConfiguration);
                return response;
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                var timeoutResponse = new DnsResponse {
                    Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverGrpc, Type = type, OriginalName = name } ],
                    Status = DnsResponseCode.ServerFailure,
                    ErrorCode = DnsQueryErrorCode.Timeout
                };
                timeoutResponse.AddServerDetails(endpointConfiguration);
                timeoutResponse.Error = $"Failed to query type {type} of \"{name}\" => The gRPC request timed out after {endpointConfiguration.TimeOut} milliseconds.";
                return timeoutResponse;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                var response = new DnsResponse {
                    Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverGrpc, Type = type, OriginalName = name } ],
                    Status = DnsResponseCode.ServerFailure,
                    ErrorCode = DnsQueryErrorCode.ServFail,
                    Exception = ex
                };
                response.AddServerDetails(endpointConfiguration);
                response.Error = $"Failed to query type {type} of \"{name}\" => {ex.Message}";
                return response;
            }
        }

        private static CancellationTokenSource CreateTimeoutTokenSource(int timeoutMilliseconds, CancellationToken cancellationToken) {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMilliseconds <= 0) {
                linkedCts.Cancel();
            } else {
                linkedCts.CancelAfter(timeoutMilliseconds);
            }
            return linkedCts;
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
                throw new DnsClientException("gRPC response is shorter than its 5-byte frame header.");
            }
            if (responseBytes[0] != 0) {
                throw new DnsClientException("Compressed gRPC DNS responses are not supported.");
            }
            uint len = ((uint)responseBytes[1] << 24) | ((uint)responseBytes[2] << 16) |
                       ((uint)responseBytes[3] << 8) | responseBytes[4];
            if (len == 0 || len > DnsHttpResponseBody.MaxWireBytes || len != responseBytes.Length - 5) {
                throw new DnsClientException("gRPC DNS response frame length does not match its body.");
            }
            var payload = new byte[(int)len];
            Buffer.BlockCopy(responseBytes, 5, payload, 0, (int)len);
            return payload;
        }

        private static void EnsureGrpcSuccess(HttpResponseMessage response) {
            string? statusValue = null;
            if (response.TrailingHeaders.TryGetValues("grpc-status", out var values)) {
                foreach (string value in values) {
                    if (statusValue != null) {
                        throw new DnsClientException("gRPC response contains multiple status trailers.");
                    }
                    statusValue = value;
                }
            }
            if (statusValue == null) {
                throw new DnsClientException("gRPC response is missing gRPC status trailer.");
            }
            if (!int.TryParse(statusValue, NumberStyles.None, CultureInfo.InvariantCulture, out int status) ||
                statusValue != status.ToString(CultureInfo.InvariantCulture)) {
                throw new DnsClientException("gRPC response has a malformed status trailer.");
            }
            if (status != 0) {
                throw new DnsClientException($"gRPC status {status} indicates failure.");
            }
        }
    }
#endif
}
