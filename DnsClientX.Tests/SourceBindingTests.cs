using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests explicit source-address binding and unsupported-transport behavior.
    /// </summary>
    [Collection("NoParallel")]
    public class SourceBindingTests {
        /// <summary>UDP queries originate from the configured local address and port.</summary>
        [Fact]
        public async Task UdpUsesConfiguredLocalEndpoint() {
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
            int sourcePort = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            IPEndPoint? observedSource = null;
            Task responder = Task.Run(async () => {
#if NET5_0_OR_GREATER
                UdpReceiveResult request = await server.ReceiveAsync(cts.Token);
#else
                Task<UdpReceiveResult> receiveTask = server.ReceiveAsync();
                Task completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, cts.Token));
                if (completed != receiveTask) {
                    throw new OperationCanceledException(cts.Token);
                }
                UdpReceiveResult request = await receiveTask;
#endif
                observedSource = request.RemoteEndPoint;
                byte[] response = TestUtilities.CreateResponseFromQuery(request.Buffer);
                await server.SendAsync(response, response.Length, request.RemoteEndPoint);
            }, cts.Token);

            var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                Port = port,
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, sourcePort),
                TimeOut = 2000
            };
            using var client = new ClientX(configuration);
            DnsResponse result = await client.Resolve("source-binding.example", retryOnTransient: false, cancellationToken: cts.Token);
            await responder;

            Assert.Equal(DnsResponseCode.NoError, result.Status);
            Assert.Equal(IPAddress.Loopback, observedSource!.Address);
            Assert.Equal(sourcePort, observedSource.Port);
        }

        /// <summary>HTTP transports fail explicitly instead of silently ignoring LocalEndPoint.</summary>
        [Fact]
        public async Task HttpTransportRejectsLocalEndPoint() {
            var configuration = new Configuration(new Uri("https://dns.example/dns-query"), DnsRequestFormat.DnsOverHttps) {
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
            };
            using var client = new ClientX(configuration);

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                client.Resolve("example.com", retryOnTransient: false));
        }

        /// <summary>RFC 2136 updates share the TCP engine and originate from the configured local endpoint.</summary>
        [Fact]
        public async Task UpdateUsesConfiguredLocalEndpoint() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            int sourcePort = TestUtilities.GetFreeTcpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            IPEndPoint? observedSource = null;
            Task responder = Task.Run(async () => {
#if NETFRAMEWORK
                using TcpClient connection = await listener.AcceptTcpClientAsync();
#else
                using TcpClient connection = await listener.AcceptTcpClientAsync(cts.Token);
#endif
                observedSource = (IPEndPoint)connection.Client.RemoteEndPoint!;
                NetworkStream stream = connection.GetStream();
                byte[] length = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, length, 2, cts.Token);
                int queryLength = (length[0] << 8) | length[1];
                byte[] query = new byte[queryLength];
                await TestUtilities.ReadExactlyAsync(stream, query, queryLength, cts.Token);
                byte[] response = TestUtilities.CreateResponseFromQuery(query, 0xA800);
                byte[] responseLength = { (byte)(response.Length >> 8), (byte)response.Length };
                await stream.WriteAsync(responseLength, 0, responseLength.Length, cts.Token);
                await stream.WriteAsync(response, 0, response.Length, cts.Token);
                listener.Stop();
            }, cts.Token);

            var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                Port = port,
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, sourcePort),
                TimeOut = 2000
            };
            using var client = new ClientX(configuration);
            DnsResponse result = await client.UpdateRecordAsync(
                "example.com",
                "www.example.com",
                DnsRecordType.A,
                "192.0.2.10",
                cancellationToken: cts.Token);
            await responder;

            Assert.Equal(DnsResponseCode.NoError, result.Status);
            Assert.Equal(IPAddress.Loopback, observedSource!.Address);
            Assert.Equal(sourcePort, observedSource.Port);
        }

        /// <summary>TCP constructs a socket for the resolved IPv6 family on every target framework.</summary>
        [Fact]
        public async Task TcpUsesResolvedIpv6AddressFamily() {
            if (!Socket.OSSupportsIPv6) return;
            var listener = new TcpListener(IPAddress.IPv6Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task responder = Task.Run(async () => {
#if NETFRAMEWORK
                using TcpClient connection = await listener.AcceptTcpClientAsync();
#else
                using TcpClient connection = await listener.AcceptTcpClientAsync(cts.Token);
#endif
                NetworkStream stream = connection.GetStream();
                byte[] length = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, length, 2, cts.Token);
                int queryLength = (length[0] << 8) | length[1];
                byte[] query = new byte[queryLength];
                await TestUtilities.ReadExactlyAsync(stream, query, queryLength, cts.Token);
                byte[] response = TestUtilities.CreateResponseFromQuery(query);
                byte[] responseLength = { (byte)(response.Length >> 8), (byte)response.Length };
                await stream.WriteAsync(responseLength, 0, responseLength.Length, cts.Token);
                await stream.WriteAsync(response, 0, response.Length, cts.Token);
                listener.Stop();
            }, cts.Token);
            Func<AddressFamily, TcpClient> previous = DnsWireResolveTcp.TcpClientFactory;
            AddressFamily? observedFamily = null;
            try {
                DnsWireResolveTcp.TcpClientFactory = family => {
                    observedFamily = family;
                    return new TcpClient(family);
                };
                var query = new DnsMessage("ipv6.example", DnsRecordType.A, requestDnsSec: false);

                byte[] response = await DnsWireResolveTcp.SendQueryOverTcp(
                    query.SerializeDnsWireFormat(),
                    IPAddress.IPv6Loopback.ToString(),
                    port,
                    2000,
                    cts.Token);
                await responder;

                Assert.NotEmpty(response);
                Assert.Equal(AddressFamily.InterNetworkV6, observedFamily);
            } finally {
                DnsWireResolveTcp.TcpClientFactory = previous;
                listener.Stop();
            }
        }
    }
}
