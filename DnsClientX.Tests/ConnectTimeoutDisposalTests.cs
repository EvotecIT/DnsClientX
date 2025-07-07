using System;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class ConnectTimeoutDisposalTests {
        [Fact]
        public async Task ConnectAsync_ShouldDisposeSocketOnTimeout() {
            MethodInfo method = typeof(ClientX).GetMethod("ConnectAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            using var tcpClient = new TcpClient();
            await Assert.ThrowsAsync<TimeoutException>(async () =>
                await (Task)method.Invoke(null, new object[] { tcpClient, "localhost", 65535, 0, CancellationToken.None })!);
            Assert.Null(tcpClient.Client);
        }
    }
}
