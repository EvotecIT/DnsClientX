using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
/// <summary>
/// Ensures TCP connections are properly disposed by the resolver.
/// </summary>
[Collection("DisposalTests")]
public class TcpDisposeCountTests {
        private class CountingTcpClient : TcpClient {
            private readonly Action _onDispose;
            public CountingTcpClient(AddressFamily family, Action onDispose) : base(family) => _onDispose = onDispose;
            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);
                if (disposing) _onDispose();
            }
        }

        private static int GetFreePort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task RunTcpServerAsync(int port, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            using var registration = token.Register(listener.Stop);
            using TcpClient client = await listener.AcceptTcpClientAsync();
            using var cancelPeer = token.Register(client.Dispose);
            using NetworkStream stream = client.GetStream();
            byte[] len = new byte[2];
            await TestUtilities.ReadExactlyAsync(stream, len, 2, token);
            if (BitConverter.IsLittleEndian) Array.Reverse(len);
            int length = BitConverter.ToUInt16(len, 0);
            byte[] buffer = new byte[length];
            await TestUtilities.ReadExactlyAsync(stream, buffer, length, token);
            byte[] response = TestUtilities.CreateResponseFromQuery(buffer);
            byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
            await stream.WriteAsync(prefix, 0, prefix.Length, token);
            await stream.WriteAsync(response, 0, response.Length, token);
            listener.Stop();
        }

        /// <summary>
        /// Verifies the TCP connection object is disposed after resolving.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatTcp_ShouldDisposeConnection() {
            int disposed = 0;
            var prevFactory = DnsWireResolveTcp.TcpClientFactory;

            try {
                DnsWireResolveTcp.TcpClientFactory = family =>
                    new CountingTcpClient(family, () => Interlocked.Increment(ref disposed));

                int port = GetFreePort();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var serverTask = RunTcpServerAsync(port, cts.Token);

                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port };
                DnsResponse response = await DnsWireResolveTcp.ResolveWireFormatTcp(
                    "127.0.0.1",
                    port,
                    "example.com",
                    DnsRecordType.A,
                    requestDnsSec: false,
                    validateDnsSec: false,
                    debug: false,
                    config,
                    cts.Token);
                await serverTask;

                Assert.Equal(DnsResponseCode.NoError, response.Status);
                Assert.Equal(1, Volatile.Read(ref disposed));
            } finally {
                // Ensure cleanup happens even if test fails
                DnsWireResolveTcp.TcpClientFactory = prevFactory;
            }
        }
    }
}
