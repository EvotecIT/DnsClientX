using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests fallback behavior from UDP to TCP queries.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsWireFallbackTests {
        private static async Task RunUdpServerAsync(UdpClient udp, bool truncated, CancellationToken token) {
            using var cancellation = token.Register(udp.Dispose);
            try {
#if NET5_0_OR_GREATER
                UdpReceiveResult result = await udp.ReceiveAsync(token).AsTask();
#else
                UdpReceiveResult result = await udp.ReceiveAsync();
#endif
                byte[] response = TestUtilities.CreateResponseFromQuery(result.Buffer, truncated ? (ushort)0x8380 : (ushort)0x8180);
                await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                // Expected when an assertion fails before the server receives a query.
            } catch (ObjectDisposedException) when (token.IsCancellationRequested) {
                // Cancellation releases the non-cancelable .NET Framework receive.
            } catch (SocketException) when (token.IsCancellationRequested) {
                // Disposing the socket can interrupt a pending receive on Windows.
            }
        }

        private static async Task RunTcpServerAsync(TcpListener listener, Action onReceived, CancellationToken token) {
            using var cancellation = token.Register(listener.Stop);
            try {
                using TcpClient client = await listener.AcceptTcpClientAsync();
                NetworkStream stream = client.GetStream();
                byte[] lengthBuffer = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, lengthBuffer, 2, token);
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBuffer);
                int length = BitConverter.ToUInt16(lengthBuffer, 0);
                byte[] queryBuffer = new byte[length];
                await TestUtilities.ReadExactlyAsync(stream, queryBuffer, length, token);
                onReceived();
                byte[] response = TestUtilities.CreateResponseFromQuery(queryBuffer);
                byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
                await stream.WriteAsync(prefix, 0, prefix.Length, token);
                await stream.WriteAsync(response, 0, response.Length, token);
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                // Expected during test shutdown.
            } catch (ObjectDisposedException) when (token.IsCancellationRequested) {
                // Cancellation can interrupt an accepted client's read.
            } catch (SocketException) when (token.IsCancellationRequested) {
                // Stopping the listener releases the non-cancelable accept.
            } finally {
                listener.Stop();
            }
        }

        /// <summary>
        /// UDP queries should automatically retry over TCP when truncated.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatUdp_ShouldFallbackToTcpWhenTruncated() {
            bool tcpCalled = false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var (udp, listener) = TestUtilities.BindUdpAndTcp();
            using var udpReservation = udp;
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            var udpTask = RunUdpServerAsync(udp, truncated: true, cts.Token);
            var tcpTask = RunTcpServerAsync(listener, () => tcpCalled = true, cts.Token);

            try {
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = port };
                DnsResponse response = await DnsWireResolveUdp.ResolveWireFormatUdp(
                    "127.0.0.1",
                    port,
                    "example.com",
                    DnsRecordType.A,
                    requestDnsSec: false,
                    validateDnsSec: false,
                    debug: false,
                    config,
                    1,
                    cts.Token);

                await Task.WhenAll(udpTask, tcpTask);
                Assert.True(tcpCalled, "Expected TCP fallback to be used");
                Assert.False(response.IsTruncated);
                Assert.Equal(Transport.Tcp, response.UsedTransport);
            } finally {
                cts.Cancel();
                listener.Stop();
                await Task.WhenAll(udpTask, tcpTask);
            }
        }

        /// <summary>
        /// Ensures TCP fallback can be disabled for UDP queries.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatUdp_ShouldNotFallbackWhenDisabled() {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(udp, truncated: true, cts.Token);

            try {
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = port, UseTcpFallback = false };
                DnsResponse response = await DnsWireResolveUdp.ResolveWireFormatUdp(
                    "127.0.0.1",
                    port,
                    "example.com",
                    DnsRecordType.A,
                    requestDnsSec: false,
                    validateDnsSec: false,
                    debug: false,
                    config,
                    1,
                    cts.Token);

                await udpTask;
                Assert.True(response.IsTruncated);
                Assert.Equal(Transport.Udp, response.UsedTransport);
            } finally {
                cts.Cancel();
                await udpTask;
            }
        }

        /// <summary>Endpoint metadata cannot overwrite the transport that supplied a fallback answer.</summary>
        [Fact]
        public async Task MultiResolverPreservesActualTcpFallbackTransport() {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var (udpSocket, listener) = TestUtilities.BindUdpAndTcp();
            using var udpReservation = udpSocket;
            int port = ((IPEndPoint)udpSocket.Client.LocalEndPoint!).Port;
            Task udp = RunUdpServerAsync(udpSocket, truncated: true, cts.Token);
            Task tcp = RunTcpServerAsync(listener, () => { }, cts.Token);
            try {
                var endpoint = new DnsResolverEndpoint { Host = "127.0.0.1", Port = port, Transport = Transport.Udp };
                using var resolver = new DnsMultiResolver(new[] { endpoint });
                DnsResponse response = await resolver.QueryAsync("example.com", DnsRecordType.A, cts.Token);
                await Task.WhenAll(udp, tcp);
                Assert.Equal(DnsResponseCode.NoError, response.Status);
                Assert.Equal(Transport.Tcp, response.UsedTransport);
                Assert.Same(endpoint, response.UsedEndpoint);
            } finally {
                cts.Cancel();
                listener.Stop();
                await Task.WhenAll(udp, tcp);
            }
        }
    }
}
