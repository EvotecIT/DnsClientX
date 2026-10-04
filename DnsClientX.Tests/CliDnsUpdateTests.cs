using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Verifies explicit-target DNS UPDATE through the actual CLI entry point.</summary>
    [Collection("NoParallel")]
    public class CliDnsUpdateTests {
        /// <summary>Explicit UDP/TCP endpoint syntax selects wire UPDATE rather than probe mode.</summary>
        [Theory]
        [InlineData("udp")]
        [InlineData("tcp")]
        public async Task Update_UsesExplicitEndpoint(string transport) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using CancellationTokenRegistration registration = deadline.Token.Register(listener.Stop);
            Task<byte[]> server = ReadAndReplyAsync(listener, deadline.Token);
            try {
                var (exitCode, output, error) = await InvokeCliAsync(
                    "--probe-endpoint", $"{transport}@127.0.0.1:{port}",
                    "--update", "example.com", "www.example.com", "A", "192.0.2.10", "--ttl", "300");

                Assert.Equal(0, exitCode);
                Assert.Contains("Update status: NoError", output);
                Assert.Equal(string.Empty, error);
                byte[] request = await server;
                Assert.Equal(5, (request[2] >> 3) & 0x0F);
                Assert.Equal(1, (request[8] << 8) | request[9]);
            } finally {
                deadline.Cancel();
                listener.Stop();
                try { await server; }
                catch (Exception ex) when (deadline.IsCancellationRequested &&
                    (ex is OperationCanceledException || ex is SocketException ||
                     ex is ObjectDisposedException || ex is InvalidOperationException)) { }
            }
        }

        /// <summary>Missing target reports a migration instruction before attempting a DNS write.</summary>
        [Fact]
        public async Task Update_WithoutExplicitEndpoint_ReportsRequiredOption() {
            var (exitCode, _, error) = await InvokeCliAsync(
                "--update", "example.com", "www.example.com", "A", "192.0.2.10");

            Assert.Equal(1, exitCode);
            Assert.Contains("--probe-endpoint", error);
        }

        private static async Task<byte[]> ReadAndReplyAsync(TcpListener listener, CancellationToken cancellationToken) {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = client.GetStream();
            byte[] prefix = new byte[2];
            await TestUtilities.ReadExactlyAsync(stream, prefix, prefix.Length, cancellationToken);
            byte[] request = new byte[(prefix[0] << 8) | prefix[1]];
            await TestUtilities.ReadExactlyAsync(stream, request, request.Length, cancellationToken);
            byte[] response = new byte[12];
            response[0] = request[0];
            response[1] = request[1];
            response[2] = 0xA8;
            await stream.WriteAsync(new byte[] { 0, 12 }, 0, 2, cancellationToken);
            await stream.WriteAsync(response, 0, response.Length, cancellationToken);
            return request;
        }

        private static async Task<(int ExitCode, string Output, string Error)> InvokeCliAsync(params string[] arguments) {
            Type program = Assembly.Load("DnsClientX.Cli").GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = program.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            using var output = new StringWriter();
            using var error = new StringWriter();
            TextWriter originalOutput = Console.Out;
            TextWriter originalError = Console.Error;
            try {
                Console.SetOut(output);
                Console.SetError(error);
                int exitCode = await (Task<int>)main.Invoke(null, new object[] { arguments })!;
                return (exitCode, output.ToString(), error.ToString());
            } finally {
                Console.SetOut(originalOutput);
                Console.SetError(originalError);
            }
        }
    }
}
