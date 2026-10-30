using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Opt-in multicast resolution on an explicitly selected loopback interface.</summary>
    public class DnsWireResolveMulticastTests {
        /// <summary>A real correlated multicast reply retains the original query and transport metadata.</summary>
        [RealDnsFact]
        public async Task ResolveWireFormatMulticast_ShouldReturnResponse() {
            IPAddress group = IPAddress.Parse("239.255.42.100");
            int interfaceIndex = NetworkInterface.GetAllNetworkInterfaces().First(item =>
                item.GetIPProperties().UnicastAddresses.Any(address => address.Address.Equals(IPAddress.Loopback)))
                .GetIPProperties().GetIPv4Properties()!.Index;
            using var server = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
            server.JoinMulticastGroup(group, IPAddress.Loopback);
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var registration = stop.Token.Register(server.Dispose);
            async Task<byte[]> ReplyAsync() {
                UdpReceiveResult packet = await server.ReceiveAsync();
                byte[] response = DnsDiscoveryTestWire.Response(packet.Buffer,
                    DnsDiscoveryTestWire.Address("example.local", "192.0.2.1"));
                await server.SendAsync(response, response.Length, packet.RemoteEndPoint);
                return packet.Buffer;
            }
            Task<byte[]> reply = ReplyAsync();
            try {
                var config = new Configuration(group.ToString(), DnsRequestFormat.Multicast) {
                    Port = port, MulticastInterfaceIndex = interfaceIndex, TimeOut = 1000
                };
                DnsResponse response = await DnsWireResolveMulticast.ResolveWireFormatMulticast(
                    group.ToString(), port, "example.local", DnsRecordType.A,
                    false, false, false, config, stop.Token);
                byte[] query = await reply;
                Assert.NotNull(response);
                Assert.NotEmpty(query);
                Assert.Equal(DnsRequestFormat.Multicast, response.Questions[0].RequestFormat);
                Assert.Equal(DnsResponseCode.NoError, response.Status);
                Assert.Equal("192.0.2.1", Assert.Single(response.Answers).Data);
                Assert.Equal(0, query[0] | query[1]);
                Assert.True(DnsWireMessageParser.TryParseQuestion(query, 0, out var question));
                Assert.Equal("example.local", question.Name);
                Assert.Equal((ushort)DnsRecordType.A, question.Type);
            } finally {
                stop.Cancel();
                try { await reply; }
                catch (Exception exception) when (stop.IsCancellationRequested
                    && exception is OperationCanceledException or SocketException or ObjectDisposedException) { }
            }
        }
    }
}
