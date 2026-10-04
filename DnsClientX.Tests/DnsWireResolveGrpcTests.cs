#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests DNS over gRPC resolution logic.
    /// </summary>
    public class DnsWireResolveGrpcTests {
        /// <summary>
        /// Ensures exceptions are reported as server failures.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatGrpc_ReturnsServerFailure_OnException() {
            var prevFactory = DnsWireResolveGrpc.ClientFactory;
            try {
                DnsWireResolveGrpc.ClientFactory = _ => throw new InvalidOperationException("boom");
                var config = new Configuration("dummy", DnsRequestFormat.DnsOverGrpc);
                var response = await DnsWireResolveGrpc.ResolveWireFormatGrpc("dummy", 443, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);
                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Contains("boom", response.Error);
            } finally {
                DnsWireResolveGrpc.ClientFactory = prevFactory;
            }
        }

        /// <summary>
        /// Verifies that the provided HTTP call delegate is invoked.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatGrpc_UsesProvidedCallFunc() {
            var prevClient = DnsWireResolveGrpc.ClientFactory;
            var prevSend = DnsWireResolveGrpc.SendAsync;
            try {
                byte[]? captured = null;
                DnsWireResolveGrpc.ClientFactory = _ => new HttpClient();
                DnsWireResolveGrpc.SendAsync = (_, request, _) => {
                    captured = request.Content?.ReadAsByteArrayAsync().Result;
                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                        Content = new ByteArrayContent(TestUtilities.CreateGrpcResponseFromRequest(captured!))
                    };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc");
                    response.TrailingHeaders.TryAddWithoutValidation("grpc-status", "0");
                    return Task.FromResult(response);
                };
                var config = new Configuration("dummy", DnsRequestFormat.DnsOverGrpc);
                var response = await DnsWireResolveGrpc.ResolveWireFormatGrpc("dummy", 443, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);
                Assert.Equal(DnsResponseCode.NoError, response.Status);
                Assert.NotNull(captured);
            } finally {
                DnsWireResolveGrpc.ClientFactory = prevClient;
                DnsWireResolveGrpc.SendAsync = prevSend;
            }
        }

        /// <summary>A valid DNS frame does not override a failed HTTP or gRPC envelope.</summary>
        [Theory]
        [InlineData(System.Net.HttpStatusCode.BadGateway, "0", "HTTP 502")]
        [InlineData(System.Net.HttpStatusCode.OK, "7", "gRPC status 7")]
        [InlineData(System.Net.HttpStatusCode.OK, null, "missing gRPC status")]
        public async Task ResolveWireFormatGrpc_RejectsFailedEnvelope(
            System.Net.HttpStatusCode httpStatus, string? grpcStatus, string expectedError) {
            var previousClient = DnsWireResolveGrpc.ClientFactory;
            var previousSend = DnsWireResolveGrpc.SendAsync;
            try {
                DnsWireResolveGrpc.ClientFactory = _ => new HttpClient();
                DnsWireResolveGrpc.SendAsync = async (_, request, _) => {
                    byte[] query = await request.Content!.ReadAsByteArrayAsync();
                    var response = new HttpResponseMessage(httpStatus) {
                        Content = new ByteArrayContent(TestUtilities.CreateGrpcResponseFromRequest(query))
                    };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc");
                    if (grpcStatus != null) {
                        response.TrailingHeaders.TryAddWithoutValidation("grpc-status", grpcStatus);
                    }
                    return response;
                };

                var config = new Configuration("dummy", DnsRequestFormat.DnsOverGrpc);
                DnsResponse response = await DnsWireResolveGrpc.ResolveWireFormatGrpc(
                    "dummy", 443, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);

                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Contains(expectedError, response.Error, StringComparison.OrdinalIgnoreCase);
            } finally {
                DnsWireResolveGrpc.ClientFactory = previousClient;
                DnsWireResolveGrpc.SendAsync = previousSend;
            }
        }

        /// <summary>A DNS-shaped body is not a gRPC reply without its declared media type.</summary>
        [Fact]
        public async Task ResolveWireFormatGrpc_RejectsNonGrpcContentType() {
            var previousClient = DnsWireResolveGrpc.ClientFactory;
            var previousSend = DnsWireResolveGrpc.SendAsync;
            try {
                DnsWireResolveGrpc.ClientFactory = _ => new HttpClient();
                DnsWireResolveGrpc.SendAsync = async (_, request, _) => {
                    byte[] query = await request.Content!.ReadAsByteArrayAsync();
                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                        Content = new ByteArrayContent(TestUtilities.CreateGrpcResponseFromRequest(query))
                    };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    response.TrailingHeaders.TryAddWithoutValidation("grpc-status", "0");
                    return response;
                };

                var config = new Configuration("dummy", DnsRequestFormat.DnsOverGrpc);
                DnsResponse response = await DnsWireResolveGrpc.ResolveWireFormatGrpc(
                    "dummy", 443, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);

                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Contains("non-gRPC content type", response.Error, StringComparison.Ordinal);
            } finally {
                DnsWireResolveGrpc.ClientFactory = previousClient;
                DnsWireResolveGrpc.SendAsync = previousSend;
            }
        }

        /// <summary>
        /// Ensures the configured endpoint timeout is honored when the gRPC call stalls.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatGrpc_ReturnsTimeout_WhenSendStalls() {
            var prevClient = DnsWireResolveGrpc.ClientFactory;
            var prevSend = DnsWireResolveGrpc.SendAsync;
            try {
                DnsWireResolveGrpc.ClientFactory = _ => new HttpClient();
                DnsWireResolveGrpc.SendAsync = async (_, _, token) => {
                    await Task.Delay(Timeout.Infinite, token);
                    throw new InvalidOperationException("unreachable");
                };

                var config = new Configuration("dummy", DnsRequestFormat.DnsOverGrpc) {
                    TimeOut = 100
                };

                var response = await DnsWireResolveGrpc.ResolveWireFormatGrpc("dummy", 443, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);
                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Equal(DnsQueryErrorCode.Timeout, response.ErrorCode);
                Assert.Contains("timed out", response.Error, StringComparison.OrdinalIgnoreCase);
            } finally {
                DnsWireResolveGrpc.ClientFactory = prevClient;
                DnsWireResolveGrpc.SendAsync = prevSend;
            }
        }

        /// <summary>A declared oversized gRPC reply is rejected before decoding its DNS frame.</summary>
        [Fact]
        public async Task ResolveWireFormatGrpc_RejectsOversizedResponse() {
            var previousClient = DnsWireResolveGrpc.ClientFactory;
            var previousSend = DnsWireResolveGrpc.SendAsync;
            try {
                DnsWireResolveGrpc.ClientFactory = _ => new HttpClient();
                DnsWireResolveGrpc.SendAsync = (_, _, _) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                    Content = new ByteArrayContent(new byte[ushort.MaxValue + 6])
                });

                var config = new Configuration("dummy", DnsRequestFormat.DnsOverGrpc);
                DnsResponse response = await DnsWireResolveGrpc.ResolveWireFormatGrpc(
                    "dummy", 443, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);

                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Contains("65540", response.Error, StringComparison.Ordinal);
            } finally {
                DnsWireResolveGrpc.ClientFactory = previousClient;
                DnsWireResolveGrpc.SendAsync = previousSend;
            }
        }

        /// <summary>A unary gRPC frame must contain exactly the declared DNS message.</summary>
        [Fact]
        public async Task ResolveWireFormatGrpc_RejectsFrameLengthMismatch() {
            var previousClient = DnsWireResolveGrpc.ClientFactory;
            var previousSend = DnsWireResolveGrpc.SendAsync;
            try {
                DnsWireResolveGrpc.ClientFactory = _ => new HttpClient();
                DnsWireResolveGrpc.SendAsync = async (_, request, _) => {
                    byte[] query = await request.Content!.ReadAsByteArrayAsync();
                    byte[] frame = TestUtilities.CreateGrpcResponseFromRequest(query);
                    frame[4]++;
                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                        Content = new ByteArrayContent(frame)
                    };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc");
                    response.TrailingHeaders.TryAddWithoutValidation("grpc-status", "0");
                    return response;
                };

                var config = new Configuration("dummy", DnsRequestFormat.DnsOverGrpc);
                DnsResponse response = await DnsWireResolveGrpc.ResolveWireFormatGrpc(
                    "dummy", 443, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);

                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Contains("frame length", response.Error, StringComparison.OrdinalIgnoreCase);
            } finally {
                DnsWireResolveGrpc.ClientFactory = previousClient;
                DnsWireResolveGrpc.SendAsync = previousSend;
            }
        }
    }
}
#endif
