using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    [Collection("NoParallel")]
    public class InvalidReferralTests {
        private static byte[] EncodeName(string name) {
            using var ms = new System.IO.MemoryStream();
            foreach (var label in name.TrimEnd('.').Split('.')) {
                var bytes = Encoding.ASCII.GetBytes(label);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }
            ms.WriteByte(0);
            return ms.ToArray();
        }

        private static string DecodeName(byte[] query, int offset, out int newOffset) {
            var labels = new System.Collections.Generic.List<string>();
            while (true) {
                int len = query[offset++];
                if (len == 0) break;
                labels.Add(Encoding.ASCII.GetString(query, offset, len));
                offset += len;
            }
            newOffset = offset;
            return string.Join(".", labels);
        }

        private static byte[] CreateReferralResponse(byte[] query) {
            string qname = DecodeName(query, 12, out int pos);
            var qtail = new byte[query.Length - 12];
            Array.Copy(query, 12, qtail, 0, qtail.Length);

            var nsNameBytes = EncodeName("invalid.example.");
            var nameBytes = EncodeName(qname);

            using var ms = new System.IO.MemoryStream();
            void WriteU16(ushort v) { ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v); }
            void WriteU32(uint v) { ms.WriteByte((byte)(v >> 24)); ms.WriteByte((byte)(v >> 16)); ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v); }

            ms.Write(query, 0, 2);            // ID
            WriteU16(0x8180);                 // flags
            WriteU16(1);                      // qd
            WriteU16(0);                      // an
            WriteU16(1);                      // ns
            WriteU16(0);                      // ar
            ms.Write(qtail, 0, qtail.Length); // original question
            ms.Write(nameBytes, 0, nameBytes.Length);
            WriteU16((ushort)DnsRecordType.NS);
            WriteU16(1);                      // class IN
            WriteU32(60);                     // ttl
            WriteU16((ushort)nsNameBytes.Length);
            ms.Write(nsNameBytes, 0, nsNameBytes.Length);
            return ms.ToArray();
        }

        private static async Task<int> RunReferralServerAsync(int port, int expected, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            int count = 0;
            while (count < expected && !token.IsCancellationRequested) {
#if NET5_0_OR_GREATER
                var receiveTask = udp.ReceiveAsync(token).AsTask();
#else
                var receiveTask = udp.ReceiveAsync();
#endif
                var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));
                if (completed == receiveTask) {
                    var result = await receiveTask;
                    var response = CreateReferralResponse(result.Buffer);
                    await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
                    count++;
                }
            }
            return count;
        }

        [Fact]
        public async Task ResolveFromRoot_StopsAfterMaxRetries() {
            const int max = 3;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunReferralServerAsync(53, max, cts.Token);

            using var client = new ClientX();
            var response = await client.ResolveFromRoot("example.com", cancellationToken: cts.Token, maxRetries: max, rootServers: new[] { "127.0.0.1" });
            int calls = await serverTask;
            cts.Cancel();

            Assert.Equal(max, calls);
            Assert.NotNull(response);
        }
    }
}
