using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests DNS over TLS resolution logic.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsWireResolveDotTests {
        private static int GetFreePort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task RunInvalidTlsServerAsync(int port, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
#if NET8_0_OR_GREATER
            using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
            using TcpClient client = await listener.AcceptTcpClientAsync();
#endif
            NetworkStream stream = client.GetStream();
            byte[] data = Encoding.ASCII.GetBytes("plain text");
            await stream.WriteAsync(data, 0, data.Length, token);
            await stream.FlushAsync(token);
            listener.Stop();
        }

        /// <summary>
        /// Ensures authentication failures are converted to <see cref="DnsClientException"/>.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatDoT_ShouldWrapAuthenticationException() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunInvalidTlsServerAsync(port, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) { Port = port };

            var ex = await Assert.ThrowsAsync<DnsClientException>(async () =>
                await DnsWireResolveDot.ResolveWireFormatDoT("127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, true, cts.Token));

            Assert.Equal(config.Hostname, ex.Response.Questions[0].HostName);
            Assert.Equal(config.Port, ex.Response.Questions[0].Port);

            await serverTask;
        }
    }
}
