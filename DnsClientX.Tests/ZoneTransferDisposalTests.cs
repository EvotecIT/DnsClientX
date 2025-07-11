using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;

namespace DnsClientX.Tests {
    public class ZoneTransferDisposalTests {
        [Fact]
        public async Task SendAxfrOverTcp_ShouldDisposeResources_OnTimeout() {
            MethodInfo method = typeof(ClientX).GetMethod(
                "SendAxfrOverTcp",
                BindingFlags.NonPublic | BindingFlags.Static)!;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync();

            using var cts = new CancellationTokenSource(500);
            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port };
            var enumerable = (IAsyncEnumerable<ZoneTransferResult>)method.Invoke(null, new object[] { new byte[] { 0, 0 }, "127.0.0.1", port, 200, false, config, cts.Token })!;
            var callTask = Task.Run(async () => {
                await foreach (var _ in enumerable) { }
            });

            TcpClient serverClient = await acceptTask;
            var buf = new byte[4];
            int read = 0;
            var stream = serverClient.GetStream();
            while (read < 4) {
                int r = await stream.ReadAsync(buf, read, 4 - read, cts.Token);
                if (r == 0) break;
                read += r;
            }

            await Assert.ThrowsAsync<TimeoutException>(async () => await callTask);

            await Task.Delay(100);
            serverClient.ReceiveTimeout = 200;
            int bytes;
            try {
                bytes = serverClient.Client.Receive(new byte[1]);
            } catch (SocketException) {
                bytes = 0;
            } finally {
                serverClient.Close();
            }

            Assert.Equal(0, bytes);
            listener.Stop();
        }
    }
}
