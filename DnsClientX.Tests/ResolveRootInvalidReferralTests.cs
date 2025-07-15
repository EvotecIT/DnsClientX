using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class ResolveRootInvalidReferralTests {
        private static byte[] EncodeName(string name) {
            using var ms = new System.IO.MemoryStream();
            foreach (string label in name.TrimEnd('.').Split('.')) {
                var bytes = System.Text.Encoding.ASCII.GetBytes(label);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }
            ms.WriteByte(0);
            return ms.ToArray();
        }

        private static byte[] CreateReferralResponse(string qname, string ns) {
            using var ms = new System.IO.MemoryStream();
            ushort id = 0x1234;
            ms.WriteByte((byte)(id >> 8));
            ms.WriteByte((byte)id);
            ushort flags = 0x8180;
            ms.WriteByte((byte)(flags >> 8));
            ms.WriteByte((byte)flags);
            ms.WriteByte(0);
            ms.WriteByte(1);
            ms.WriteByte(0);
            ms.WriteByte(0);
            ms.WriteByte(0);
            ms.WriteByte(1);
            ms.WriteByte(0);
            ms.WriteByte(0);
            byte[] qn = EncodeName(qname);
            ms.Write(qn, 0, qn.Length);
            ms.WriteByte(0);
            ms.WriteByte(1);
            ms.WriteByte(0);
            ms.WriteByte(1);
            byte[] authName = EncodeName(qname);
            ms.Write(authName, 0, authName.Length);
            ms.WriteByte(0);
            ms.WriteByte(2);
            ms.WriteByte(0);
            ms.WriteByte(1);
            ms.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
            byte[] nsBytes = EncodeName(ns);
            ms.WriteByte((byte)(nsBytes.Length >> 8));
            ms.WriteByte((byte)(nsBytes.Length & 0xFF));
            ms.Write(nsBytes, 0, nsBytes.Length);
            return ms.ToArray();
        }

        private static async Task RunReferralServerAsync(int port, byte[] response, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            using var reg = token.Register(() => udp.Close());
            try {
                while (!token.IsCancellationRequested) {
                    UdpReceiveResult result;
                    try {
                        result = await udp.ReceiveAsync();
                    } catch (ObjectDisposedException) when (token.IsCancellationRequested) {
                        break;
                    } catch (SocketException) when (token.IsCancellationRequested) {
                        break;
                    }
                    await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
                }
            } catch (ObjectDisposedException) when (token.IsCancellationRequested) {
                // ignore
            }
        }

        [Fact]
        public async Task ResolveFromRoot_ReturnsLastResponse_OnInvalidReferrals() {
            string[] original = RootServers.Servers.ToArray();
            for (int i = 0; i < RootServers.Servers.Length; i++) {
                RootServers.Servers[i] = "127.0.0.1";
            }

            byte[] resp = CreateReferralResponse("example.com", "invalid.");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            const int testPort = 53000;
            var serverTask = RunReferralServerAsync(testPort, resp, cts.Token);
            try {
                using var client = new ClientX();
                DnsResponse response = await client.ResolveFromRoot("example.com", maxRetries: 2, port: testPort, cancellationToken: cts.Token);
                Assert.Empty(response.Answers);
                Assert.Equal("invalid.", response.Authorities.Single().Data);
            } finally {
                cts.Cancel();
                await serverTask;
                for (int i = 0; i < original.Length; i++) {
                    RootServers.Servers[i] = original[i];
                }
            }
        }
    }
}