using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests imported endpoint loading and parsing helpers.
    /// </summary>
    [Collection("NoParallel")]
    public class EndpointParserImportTests {
        private static async Task RunHttpServerAsync(int port, string body, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            try {
                TcpClient client;
#if NET8_0_OR_GREATER
                client = await listener.AcceptTcpClientAsync(token);
#else
                var acceptTask = listener.AcceptTcpClientAsync();
                var completed = await Task.WhenAny(acceptTask, Task.Delay(Timeout.Infinite, token));
                if (completed != acceptTask) {
                    throw new OperationCanceledException(token);
                }

                client = acceptTask.Result;
#endif

                using (client)
                using (NetworkStream stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true)) {
                    while (true) {
                        string? line = await reader.ReadLineAsync();
                        if (line is null || line.Length == 0) {
                            break;
                        }
                    }

                    byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                    string headers =
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: text/plain; charset=utf-8\r\n" +
                        $"Content-Length: {bodyBytes.Length}\r\n" +
                        "Connection: close\r\n" +
                        "\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
#if NET5_0_OR_GREATER
                    await stream.WriteAsync(headerBytes, token);
                    await stream.WriteAsync(bodyBytes, token);
#else
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
#endif
                }
            } finally {
                listener.Stop();
            }
        }

        /// <summary>
        /// Ensures imported resolver content ignores comments and expands comma-separated values.
        /// </summary>
        [Fact]
        public void ParseImportedEntries_SkipsCommentsAndSplitsCommaSeparatedLines() {
            string content = "# comment\r\n; comment\r\n// comment\r\n\r\nudp@1.1.1.1:53, tcp@1.0.0.1:53\r\ndoh@https://dns.google/dns-query\r\n";

            string[] entries = new System.Collections.Generic.List<string>(EndpointParser.ParseImportedEntries(content)).ToArray();

            Assert.Equal(3, entries.Length);
            Assert.Equal("udp@1.1.1.1:53", entries[0]);
            Assert.Equal("tcp@1.0.0.1:53", entries[1]);
            Assert.Equal("doh@https://dns.google/dns-query", entries[2]);
        }

        /// <summary>
        /// Ensures file and URL imports merge with inline inputs and de-duplicate repeated endpoints.
        /// </summary>
        [Fact]
        public async Task LoadInputsAsync_MergesInlineFileAndUrlEntries() {
            string resolverFile = Path.GetTempFileName();
            File.WriteAllText(resolverFile, "udp@1.1.1.1:53\r\n# comment\r\ntcp@1.0.0.1:53\r\n");

            int port = TestUtilities.GetFreeTcpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunHttpServerAsync(port, "udp@1.1.1.1:53\r\ndoh@https://dns.google/dns-query\r\n", cts.Token);

            try {
                string[] inputs = await EndpointParser.LoadInputsAsync(
                    new[] { "udp@1.1.1.1:53", "quic@dns.adguard-dns.com:853" },
                    new[] { resolverFile },
                    new[] { $"http://127.0.0.1:{port}/resolvers.txt" },
                    cts.Token);

                Assert.Equal(4, inputs.Length);
                Assert.Contains("udp@1.1.1.1:53", inputs);
                Assert.Contains("tcp@1.0.0.1:53", inputs);
                Assert.Contains("doh@https://dns.google/dns-query", inputs);
                Assert.Contains("quic@dns.adguard-dns.com:853", inputs);
            } finally {
                File.Delete(resolverFile);
            }

            await serverTask;
        }
    }
}
