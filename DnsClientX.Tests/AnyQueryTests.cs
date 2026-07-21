using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Tests RFC ANY response projection.</summary>
    public class AnyQueryTests {
        /// <summary>An ANY query retains ordinary returned RRsets without requiring returnAllTypes.</summary>
        [Fact]
        public async Task ResolveAnyRetainsReturnedTypes() {
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
            using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(5));
            Task responder = Task.Run(async () => {
                UdpReceiveResult request = await server.ReceiveAsync();
                byte[] question = TestUtilities.CreateResponseFromQuery(request.Buffer);
                question[6] = 0;
                question[7] = 1;
                using var output = new MemoryStream();
                output.Write(question, 0, question.Length);
                byte[] answer = {
                    0xC0, 0x0C,
                    0x00, 0x01,
                    0x00, 0x01,
                    0x00, 0x00, 0x00, 0x3C,
                    0x00, 0x04,
                    192, 0, 2, 50
                };
                output.Write(answer, 0, answer.Length);
                byte[] response = output.ToArray();
                await server.SendAsync(response, response.Length, request.RemoteEndPoint);
            }, cts.Token);
            var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                Port = port,
                TimeOut = 2000
            };
            using var client = new ClientX(configuration);

            DnsResponse response = await client.Resolve("any.example", DnsRecordType.ANY,
                retryOnTransient: false, cancellationToken: cts.Token);
            await responder;

            Assert.Single(response.Answers);
            Assert.Equal(DnsRecordType.A, response.Answers[0].Type);
        }
    }
}
