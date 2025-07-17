using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests handling of invalid referral responses when resolving from the DNS root.
    /// </summary>
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

        private static string DecodeName(byte[] data) {
            var labels = new System.Collections.Generic.List<string>();
            int index = 0;
            while (index < data.Length && data[index] != 0) {
                int len = data[index++];
                labels.Add(System.Text.Encoding.ASCII.GetString(data, index, len));
                index += len;
            }
            return string.Join(".", labels);
        }

        private static byte[] CreateReferralResponse(string qname, string ns) {
            using var ms = new System.IO.MemoryStream();
            ushort id = 0x1234;
            ms.WriteByte((byte)(id >> 8));
            ms.WriteByte((byte)(id & 0xFF));
            ushort flags = 0x8180;
            ms.WriteByte((byte)(flags >> 8));
            ms.WriteByte((byte)(flags & 0xFF));
            ms.WriteByte(0); ms.WriteByte(1); // QDCOUNT
            ms.WriteByte(0); ms.WriteByte(0); // ANCOUNT
            ms.WriteByte(0); ms.WriteByte(1); // NSCOUNT
            ms.WriteByte(0); ms.WriteByte(0); // ARCOUNT

            byte[] qn = EncodeName(qname);
            ms.Write(qn, 0, qn.Length);
            ms.WriteByte(0); ms.WriteByte(1); // QTYPE A
            ms.WriteByte(0); ms.WriteByte(1); // QCLASS IN

            // authority record
            ms.WriteByte(0xC0); ms.WriteByte(0x0C); // pointer to qname
            ms.WriteByte(0); ms.WriteByte(2); // TYPE NS
            ms.WriteByte(0); ms.WriteByte(1); // CLASS IN
            ms.Write(new byte[] { 0, 0, 0, 0 }, 0, 4); // TTL
            byte[] nsBytes = EncodeName(ns);
            ms.WriteByte((byte)(nsBytes.Length >> 8));
            ms.WriteByte((byte)(nsBytes.Length & 0xFF));
            ms.Write(nsBytes, 0, nsBytes.Length);
            return ms.ToArray();
        }

        private static async Task RunReferralServerAsync(UdpClient udp, string referralNs, CancellationToken token) {
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
                    byte[] resp = CreateReferralResponse("example.com", referralNs);
                    await udp.SendAsync(resp, resp.Length, result.RemoteEndPoint);
                }
            } catch (ObjectDisposedException) when (token.IsCancellationRequested) {
                // ignore
            }
        }

        /// <summary>
        /// Ensures that the last response is returned when referrals are invalid.
        /// </summary>
        [Fact]
        public async Task ResolveFromRoot_ReturnsLastResponse_OnInvalidReferrals() {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int testPort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            var serverTask = RunReferralServerAsync(udp, "invalid.", cts.Token);
            try {
                using var client = new ClientX();
                DnsResponse response = await client.ResolveFromRoot(
                    "example.com",
                    servers: new[] { "127.0.0.1" },
                    maxRetries: 2,
                    port: testPort,
                    cancellationToken: cts.Token);

                Assert.Empty(response.AnswersMinimal);

                string authority = DecodeName(Convert.FromBase64String(response.Authorities.Single().DataRaw)) + ".";
                Assert.Equal("invalid.", authority);
                Assert.Equal(1, response.RetryCount);
            } finally {
                cts.Cancel();
                await serverTask;
            }
        }
    }
}