using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Error scenarios for DnsMultiResolver.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsMultiResolverErrorHandlingTests {
        /// <summary>Caller cancellation remains cancellation for every strategy and batch surface.</summary>
        [Theory]
        [InlineData(MultiResolverStrategy.FirstSuccess)]
        [InlineData(MultiResolverStrategy.FastestWins)]
        [InlineData(MultiResolverStrategy.SequentialFallback)]
        [InlineData(MultiResolverStrategy.RoundRobin)]
        [InlineData(MultiResolverStrategy.Random)]
        public async Task CallerCancellationPropagates(MultiResolverStrategy strategy) {
            using var socket = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            int port = ((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port;
            using var resolver = new DnsMultiResolver(new[] {
                new DnsResolverEndpoint { Host = "127.0.0.1", Port = port, Transport = Transport.Udp }
            }, new MultiResolverOptions { Strategy = strategy, DefaultTimeout = TimeSpan.FromSeconds(3), EnableFastestCache = false });
            using var cancel = new CancellationTokenSource();
            Task<DnsResponse> query = resolver.QueryAsync("cancel.example", DnsRecordType.A, cancel.Token);
            await socket.ReceiveAsync();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.QueryAllAsync("cancel.example", DnsRecordType.A, cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.QueryBatchAsync(new[] { "cancel.example" }, DnsRecordType.A, cancel.Token));
        }

        /// <summary>Endpoint deadlines continue to return a classified timeout response.</summary>
        [Fact]
        public async Task EndpointDeadlineRemainsTimeout() {
            using var socket = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            using var resolver = new DnsMultiResolver(new[] { new DnsResolverEndpoint {
                Host = "127.0.0.1", Port = ((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port,
                Transport = Transport.Udp, Timeout = TimeSpan.FromMilliseconds(50)
            } });
            DnsResponse response = await resolver.QueryAsync("timeout.example", DnsRecordType.A);
            Assert.Equal(DnsQueryErrorCode.Timeout, response.ErrorCode);
        }

        /// <summary>A thrown transport timeout has the same category as an endpoint deadline.</summary>
        [Fact]
        public async Task ThrownTransportTimeoutRemainsTimeout() {
            try {
                DnsMultiResolver.ResolveOverride = (_, _, _, _) => throw new TimeoutException("transport deadline");
                using var resolver = new DnsMultiResolver(new[] {
                    new DnsResolverEndpoint { Host = "127.0.0.1", Port = 53, Transport = Transport.Udp }
                });

                DnsResponse response = await resolver.QueryAsync("timeout.example", DnsRecordType.A);

                Assert.Equal(DnsQueryErrorCode.Timeout, response.ErrorCode);
                Assert.IsType<TimeoutException>(response.Exception);
            } finally {
                DnsMultiResolver.ResolveOverride = null;
            }
        }

        /// <summary>Rich transport failures retain their diagnostics without mutating a shared exception response.</summary>
        [Theory]
        [InlineData(DnsQueryErrorCode.Timeout)]
        [InlineData(DnsQueryErrorCode.Network)]
        [InlineData(DnsQueryErrorCode.InvalidResponse)]
        public async Task ClassifiedExceptionResponseIsPreservedAndIsolated(DnsQueryErrorCode code) {
            Exception cause = code switch {
                DnsQueryErrorCode.Timeout => new TimeoutException("transport deadline"),
                DnsQueryErrorCode.Network => new SocketException((int)SocketError.NetworkUnreachable),
                _ => new System.IO.InvalidDataException("invalid DNS message")
            };
            var failure = new DnsResponse {
                Status = DnsResponseCode.ServerFailure,
                ErrorCode = code,
                Error = "original failure",
                Exception = cause,
                UsedTransport = Transport.Tcp,
                EdnsNsid = new byte[] { 0, 255 }
            };
            failure.AddServerDetails(new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                Hostname = "127.0.0.1"
            }, Transport.Tcp);
            try {
                DnsMultiResolver.ResolveOverride = (_, _, _, _) =>
                    throw new DnsClientException("wrapped failure", cause) { Response = failure };
                var endpoint = new DnsResolverEndpoint { Host = "127.0.0.1", Port = 53, Transport = Transport.Udp };
                using var resolver = new DnsMultiResolver(new[] { endpoint });

                DnsResponse response = await resolver.QueryAsync("timeout.example", DnsRecordType.A);

                Assert.Equal(code, response.ErrorCode);
                Assert.Same(cause, response.Exception);
                Assert.Equal("original failure", response.Error);
                Assert.Equal(Transport.Tcp, response.UsedTransport);
                Assert.Equal("127.0.0.1", response.ServerAddress);
                Assert.Equal(failure.EdnsNsid, response.EdnsNsid);
                Assert.Same(endpoint, response.UsedEndpoint);
                Assert.NotSame(failure, response);
                Assert.Null(failure.UsedEndpoint);
                Assert.Equal(TimeSpan.Zero, failure.RoundTripTime);
            } finally {
                DnsMultiResolver.ResolveOverride = null;
            }
        }

        /// <summary>The shared diagnostic runner must not turn cancellation into a completed failed probe.</summary>
        [Fact]
        public async Task ProbeRunnerPropagatesCancellationDuringQuery() {
            using var socket = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            var target = new ResolverExecutionTarget { ExplicitEndpoint = new DnsResolverEndpoint {
                Host = "127.0.0.1", Port = ((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port, Transport = Transport.Udp
            } };
            using var cancel = new CancellationTokenSource();
            Task<ResolverQueryAttemptResult[]> query = ResolverProbeRunner.RunAsync(new[] { target }, "cancel.example",
                DnsRecordType.A, new ResolverQueryRunOptions { TimeoutMs = 3000 }, cancellationToken: cancel.Token);
            await socket.ReceiveAsync();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);
        }
        /// <summary>
        /// Simulates a SocketException to ensure ErrorCode is set to Network.
        /// </summary>
        [Fact]
        public async Task Error_Network_Sets_ErrorCode_Network() {
            try {
                var eps = new[] { new DnsResolverEndpoint { Host="n1", Port = 53, Transport = Transport.Udp } };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.SequentialFallback };
                DnsMultiResolver.ResolveOverride = (ep, name, type, ct) => throw new SocketException((int)SocketError.NetworkUnreachable);
                var mr = new DnsMultiResolver(eps, opts);
                var res = await mr.QueryAsync("x.com", DnsRecordType.A);
                Assert.Equal(DnsQueryErrorCode.Network, res.ErrorCode);
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }

        /// <summary>
        /// Simulates a DnsClientException to ensure ErrorCode is set to InvalidResponse.
        /// </summary>
        [Fact]
        public async Task Error_InvalidResponse_Sets_ErrorCode_InvalidResponse() {
            try {
                var eps = new[] { new DnsResolverEndpoint { Host="n1", Port=53, Transport=Transport.Udp } };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.SequentialFallback };
                DnsMultiResolver.ResolveOverride = (ep, name, type, ct) => throw new DnsClientException("bad response");
                var mr = new DnsMultiResolver(eps, opts);
                var res = await mr.QueryAsync("x.com", DnsRecordType.A);
                Assert.Equal(DnsQueryErrorCode.InvalidResponse, res.ErrorCode);
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }
    }
}
