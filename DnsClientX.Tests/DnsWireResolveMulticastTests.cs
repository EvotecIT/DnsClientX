using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsWireResolveMulticastTests {
        private static byte[] CreateDnsHeader() {
            byte[] bytes = new byte[12];
            ushort id = 0x1234;
            bytes[0] = (byte)(id >> 8);
            bytes[1] = (byte)(id & 0xFF);
            ushort flags = 0x8180;
            bytes[2] = (byte)(flags >> 8);
            bytes[3] = (byte)(flags & 0xFF);
            return bytes;
        }

        private static async Task<byte[]> RunMulticastServerAsync(byte[] response, CancellationToken token) {
            using var server = new UdpClient(AddressFamily.InterNetwork);
            server.ExclusiveAddressUse = false;
            server.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            server.Client.Bind(new IPEndPoint(IPAddress.Any, 5353));
            server.JoinMulticastGroup(IPAddress.Parse("224.0.0.251"));
            UdpReceiveResult result = await server.ReceiveAsync();
            await server.SendAsync(response, response.Length, result.RemoteEndPoint);
            return result.Buffer;
        }

        [Fact]
        public async Task ResolveWireFormatMulticast_ShouldReturnResponse() {
            var response = CreateDnsHeader();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunMulticastServerAsync(response, cts.Token);

            var config = new Configuration("224.0.0.251", DnsRequestFormat.Multicast);
            Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveMulticast")!;
            MethodInfo method = type.GetMethod("ResolveWireFormatMulticast", BindingFlags.Static | BindingFlags.NonPublic)!;
            var task = (Task<DnsResponse>)method.Invoke(null, new object[] { "224.0.0.251", 5353, "example.local", DnsRecordType.A, false, false, false, config, cts.Token })!;
            DnsResponse dnsResponse = await task;
            byte[] query = await serverTask;

            Assert.NotNull(dnsResponse);
            Assert.NotEmpty(query);
            Assert.Equal(DnsRequestFormat.Multicast, dnsResponse.Questions[0].RequestFormat);
        }
    }
}
