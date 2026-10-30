using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Checks the actual client socket after a direct TCP exchange fails.</summary>
    [Collection("NoParallel")]
    public class TcpCleanupRegressionTests {
        /// <summary>A peer closing without a DNS reply causes failure and releases the owned socket.</summary>
        [Fact]
        public async Task TcpFailure_ShouldCloseSocket() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var registration = stop.Token.Register(listener.Stop);
            async Task ClosePeerAsync() {
                using TcpClient peer = await listener.AcceptTcpClientAsync();
            }
            Task server = ClosePeerAsync();
            var previous = DnsWireResolveTcp.TcpClientFactory;
            TcpClient? observed = null;
            Socket? observedSocket = null;
            try {
                DnsWireResolveTcp.TcpClientFactory = family => {
                    observed = new TcpClient(family);
                    observedSocket = observed.Client;
                    return observed;
                };
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                    Port = port, TimeOut = 200
                };
                DnsResponse response = await DnsWireResolveTcp.ResolveWireFormatTcp(
                    "127.0.0.1", port, "example.com", DnsRecordType.A,
                    false, false, false, config, stop.Token);
                await server;
                Assert.NotEqual(DnsResponseCode.NoError, response.Status);
                Assert.NotEqual(DnsQueryErrorCode.None, response.ErrorCode);
                Assert.NotNull(response.Exception);
                Assert.NotNull(observed);
                Assert.NotNull(observedSocket);
                Assert.Throws<ObjectDisposedException>(() =>
                    observedSocket!.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Type));
            } finally {
                DnsWireResolveTcp.TcpClientFactory = previous;
                observed?.Dispose();
                stop.Cancel();
                listener.Stop();
                try { await server; }
                catch (Exception exception) when (stop.IsCancellationRequested
                    && exception is OperationCanceledException or SocketException or ObjectDisposedException) { }
            }
        }
    }
}
