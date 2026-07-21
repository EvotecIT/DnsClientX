using System.Net;
using System.Net.Sockets;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Tests UDP socket reuse and contamination safeguards.</summary>
    public class DnsUdpClientPoolTests {
        /// <summary>Successful sockets are reused while failed leases are discarded.</summary>
        [Fact]
        public void ReusesOnlySuccessfulLease() {
            using var pool = new DnsUdpClientPool();
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;

            UdpClient first = pool.Rent(IPAddress.Loopback, port, null, out DnsUdpClientPool.PoolKey firstKey);
            pool.Return(firstKey, first, reusable: true);

            UdpClient reused = pool.Rent(IPAddress.Loopback, port, null, out DnsUdpClientPool.PoolKey reusedKey);
            Assert.Same(first, reused);
            pool.Return(reusedKey, reused, reusable: false);

            UdpClient replacement = pool.Rent(IPAddress.Loopback, port, null, out DnsUdpClientPool.PoolKey replacementKey);
            Assert.NotSame(first, replacement);
            pool.Return(replacementKey, replacement, reusable: true);
        }
    }
}
