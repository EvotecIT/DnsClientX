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
                        Content = new ByteArrayContent(new byte[] { 0,0,0,0,12,0x00,0x01,0x81,0x80,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00 })
                    };
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
    }
}
#endif
