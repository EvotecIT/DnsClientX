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

        private static async Task RunRawHttpServerAsync(int port, byte[] responseBytes, CancellationToken token,
            Task? holdConnectionOpen = null, TaskCompletionSource<bool>? responseSent = null) {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            try {
#if NET8_0_OR_GREATER
                using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
                Task completed = await Task.WhenAny(acceptTask, Task.Delay(Timeout.Infinite, token));
                if (completed != acceptTask) throw new OperationCanceledException(token);
                using TcpClient client = acceptTask.Result;
#endif
                using NetworkStream stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                while (true) {
                    string? line = await reader.ReadLineAsync();
                    if (line is null || line.Length == 0) break;
                }
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length, token);
                responseSent?.TrySetResult(true);
                if (holdConnectionOpen != null) await holdConnectionOpen;
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

        /// <summary>
        /// Ensures oversized resolver import files are rejected before loading them into memory.
        /// </summary>
        [Fact]
        public async Task LoadInputsAsync_RejectsOversizedResolverFiles() {
            string resolverFile = Path.GetTempFileName();

            try {
                File.WriteAllText(resolverFile, new string('a', 300_000));

                InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => EndpointParser.LoadInputsAsync(
                    files: new[] { resolverFile }));

                Assert.Contains("import limit", exception.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                File.Delete(resolverFile);
            }
        }

        /// <summary>A declared over-limit URL body is rejected from headers before it is buffered.</summary>
        [Fact]
        public async Task LoadInputsAsync_RejectsOversizedDeclaredUrlContent() {
            int port = TestUtilities.GetFreeTcpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            byte[] responseBytes = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 300000\r\nConnection: close\r\n\r\n");
            Task server = RunRawHttpServerAsync(port, responseBytes, cts.Token);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                EndpointParser.LoadInputsAsync(urls: new[] { $"http://127.0.0.1:{port}/resolvers.txt" }, cancellationToken: cts.Token));

            Assert.Contains("import limit", exception.Message, StringComparison.OrdinalIgnoreCase);
            await server;
        }

        /// <summary>The validation API reports the same URL byte-limit failure with source context.</summary>
        [Fact]
        public async Task ValidateManyAsync_ReportsOversizedDeclaredUrlContent() {
            int port = TestUtilities.GetFreeTcpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            byte[] responseBytes = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 300000\r\nConnection: close\r\n\r\n");
            Task server = RunRawHttpServerAsync(port, responseBytes, cts.Token);

            ResolverEndpointValidationResult[] results = await EndpointParser.ValidateManyAsync(
                urls: new[] { $"http://127.0.0.1:{port}/resolvers.txt" }, cancellationToken: cts.Token);

            ResolverEndpointValidationResult result = Assert.Single(results);
            Assert.False(result.IsValid);
            Assert.Contains("import limit", result.Error, StringComparison.OrdinalIgnoreCase);
            await server;
        }

        /// <summary>A chunked response is bounded by received bytes before its final chunk.</summary>
        [Fact]
        public async Task LoadInputsAsync_RejectsOversizedStreamingUrlContent() {
            int port = TestUtilities.GetFreeTcpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var releaseServer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            string body = new string('x', 262145);
            byte[] responseBytes = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n" +
                body.Length.ToString("X") + "\r\n" + body + "\r\n");
            Task server = RunRawHttpServerAsync(port, responseBytes, cts.Token, releaseServer.Task);

            try {
                InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    EndpointParser.LoadInputsAsync(urls: new[] { $"http://127.0.0.1:{port}/resolvers.txt" }, cancellationToken: cts.Token));
                Assert.Contains("import limit", exception.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                releaseServer.TrySetResult(true);
                await server;
            }
        }

        /// <summary>The URL limit measures UTF-8 bytes rather than decoded character count.</summary>
        [Fact]
        public async Task LoadInputsAsync_RejectsMultibyteContentBeyondByteLimit() {
            int port = TestUtilities.GetFreeTcpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            byte[] body = Encoding.UTF8.GetBytes(new string('é', 140000));
            byte[] header = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n" +
                body.Length.ToString("X") + "\r\n");
            byte[] footer = Encoding.ASCII.GetBytes("\r\n0\r\n\r\n");
            byte[] responseBytes = new byte[header.Length + body.Length + footer.Length];
            Buffer.BlockCopy(header, 0, responseBytes, 0, header.Length);
            Buffer.BlockCopy(body, 0, responseBytes, header.Length, body.Length);
            Buffer.BlockCopy(footer, 0, responseBytes, header.Length + body.Length, footer.Length);
            Task server = RunRawHttpServerAsync(port, responseBytes, cts.Token);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                EndpointParser.LoadInputsAsync(urls: new[] { $"http://127.0.0.1:{port}/resolvers.txt" }, cancellationToken: cts.Token));

            Assert.Contains("import limit", exception.Message, StringComparison.OrdinalIgnoreCase);
            await server;
        }

        /// <summary>Validation reports a truncated URL and keeps processing later valid sources.</summary>
        [Fact]
        public async Task ValidateManyAsync_ContinuesAfterTruncatedUrlBody() {
            int brokenPort = TestUtilities.GetFreeTcpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            byte[] brokenResponse = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 100\r\nConnection: close\r\n\r\nudp@1.1.1.1:53");
            Task brokenServer = RunRawHttpServerAsync(brokenPort, brokenResponse, cts.Token);
            int validPort = TestUtilities.GetFreeTcpPort();
            Task validServer = RunHttpServerAsync(validPort, "udp@1.0.0.1:53\r\n", cts.Token);

            ResolverEndpointValidationResult[] results = await EndpointParser.ValidateManyAsync(
                urls: new[] {
                    $"http://127.0.0.1:{brokenPort}/broken.txt",
                    $"http://127.0.0.1:{validPort}/valid.txt"
                }, cancellationToken: cts.Token);

            Assert.Equal(2, results.Length);
            Assert.False(results[0].IsValid);
            Assert.Contains($":{brokenPort}/", results[0].Source);
            Assert.True(results[1].IsValid);
            Assert.Equal("udp@1.0.0.1:53", results[1].Entry);
            await Task.WhenAll(brokenServer, validServer);
        }

        /// <summary>Caller cancellation interrupts a body that stalls after its headers.</summary>
        [Fact]
        public async Task LoadInputsAsync_CancelsStalledUrlBody() {
            int port = TestUtilities.GetFreeTcpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var releaseServer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var responseSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] responseBytes = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 100\r\nConnection: close\r\n\r\n");
            Task server = RunRawHttpServerAsync(port, responseBytes, cts.Token, releaseServer.Task, responseSent);

            try {
                Task<string[]> load = EndpointParser.LoadInputsAsync(
                    urls: new[] { $"http://127.0.0.1:{port}/stalled.txt" }, cancellationToken: cts.Token);
                Task first = await Task.WhenAny(responseSent.Task, Task.Delay(TimeSpan.FromSeconds(5), cts.Token));
                Assert.Same(responseSent.Task, first);
                cts.CancelAfter(TimeSpan.FromMilliseconds(300));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
            } finally {
                releaseServer.TrySetResult(true);
                await server;
            }
        }
    }
}
