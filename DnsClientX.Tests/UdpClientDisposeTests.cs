using System;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    public class UdpClientDisposeTests {
        private class DisposableUdpClient : UdpClient {
            private readonly Action _onDispose;
            public DisposableUdpClient(Action onDispose) {
                _onDispose = onDispose;
            }
            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);
                _onDispose();
            }
        }

        [Fact]
        public async Task SendQueryOverUdp_ShouldDisposeClientOnException() {
            bool disposed = false;
            var originalFactory = DnsWireResolveUdp.UdpClientFactory;
            DnsWireResolveUdp.UdpClientFactory = () => new DisposableUdpClient(() => disposed = true);
            var method = typeof(DnsWireResolveUdp).GetMethod("SendQueryOverUdp", BindingFlags.NonPublic | BindingFlags.Static)!;
            var parameters = new object[] { Array.Empty<byte>(), "bad ip", 53, 100, CancellationToken.None };
            Task Invoke() => (Task)method.Invoke(null, parameters)!;
            await Assert.ThrowsAsync<FormatException>(Invoke);
            Assert.True(disposed);
            DnsWireResolveUdp.UdpClientFactory = originalFactory;
        }
    }
}
