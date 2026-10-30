using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests verifying cancellation behavior during DNS queries.
    /// </summary>
    [Collection("DisposalTests")]
    public class CancellationTests {
        private class DelayingHandler : HttpMessageHandler {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                await Task.Delay(5000, cancellationToken);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
            }
        }

        /// <summary>
        /// Ensures that <see cref="ClientX.Resolve(string,DnsRecordType,bool,bool,bool,bool,int,int,bool,bool,System.Threading.CancellationToken)"/> respects early cancellation.
        /// </summary>
        [Fact]
        public async Task ResolveShouldCancelEarly() {
            var handler = new DelayingHandler();
            using var clientX = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttps);

            var customClient = new HttpClient(handler) { BaseAddress = clientX.EndpointConfiguration.BaseUri };
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);

            using var cts = new CancellationTokenSource(100);
            await Assert.ThrowsAsync<TaskCanceledException>(() => clientX.Resolve("example.com", DnsRecordType.A, cancellationToken: cts.Token));
        }

        /// <summary>
        /// Verifies that <see cref="ClientX.QueryDns(string,DnsRecordType,DnsEndpoint,DnsSelectionStrategy,int,bool,int,int,bool,bool,bool,bool,System.Threading.CancellationToken)"/> throws when the token is already cancelled.
        /// </summary>
        [Fact]
        public async Task QueryDnsShouldCancelEarly() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(() => ClientX.QueryDns("example.com", DnsRecordType.A, cancellationToken: cts.Token));
        }

        /// <summary>
        /// Confirms that the overload accepting an array of names respects a cancelled token.
        /// </summary>
        [Fact]
        public async Task QueryDns_ArrayNames_ShouldCancelEarly() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(
                () => ClientX.QueryDns(new[] { "example.com" }, DnsRecordType.A, cancellationToken: cts.Token));
        }

        /// <summary>
        /// Confirms that the overload accepting an array of types respects a cancelled token.
        /// </summary>
        [Fact]
        public async Task QueryDns_ArrayTypes_ShouldCancelEarly() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(
                () => ClientX.QueryDns(new[] { "example.com" }, new[] { DnsRecordType.A }, cancellationToken: cts.Token));
        }

        /// <summary>
        /// Ensures the underlying client is disposed when cancellation occurs.
        /// </summary>
        [Fact]
        public async Task QueryDns_ShouldDisposeClient_WhenCancelled() {
            using var alreadyCancelled = new CancellationTokenSource();
            alreadyCancelled.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(() => ClientX.QueryDns(
                "example.com", DnsRecordType.A, cancellationToken: alreadyCancelled.Token));

            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.A },
                Servers = new[] { "127.0.0.1" },
                Port = ((IPEndPoint)server.Client.LocalEndPoint!).Port,
                RequestFormat = DnsRequestFormat.DnsOverUDP,
                TimeOutMilliseconds = 5000,
                RetryCount = 1
            };
            Task<DnsResponse[]> query = ClientX.QueryDns(request, cts.Token);
            try {
                Task<UdpReceiveResult> receive = server.ReceiveAsync();
                Assert.Same(receive, await Task.WhenAny(receive, Task.Delay(TimeSpan.FromSeconds(3))));
                UdpReceiveResult packet = await receive;
                cts.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);

                // The local port belongs to this operation, unlike the process-wide disposal counter.
                using var rebound = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                rebound.ExclusiveAddressUse = true;
                rebound.Bind(packet.RemoteEndPoint);
            } finally {
                cts.Cancel();
                try { await query; } catch (OperationCanceledException) { }
            }
        }

        /// <summary>
        /// Ensures that providing an already cancelled token results in an <see cref="OperationCanceledException"/>.
        /// </summary>
        [Fact]
        public async Task Resolve_AlreadyCancelledToken_ShouldThrow() {
            using var client = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttps);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => client.Resolve("example.com", DnsRecordType.A, cancellationToken: cts.Token));
        }
    }
}
