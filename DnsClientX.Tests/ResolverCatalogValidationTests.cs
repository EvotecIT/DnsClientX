using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests resolver catalog validation workflows.
    /// </summary>
    [Collection("NoParallel")]
    public class ResolverCatalogValidationTests {
        private const string CloudflareStamp = "sdns://AgUAAAAAAAAABzEuMS4xLjEAGm1vemlsbGEuY2xvdWRmbGFyZS1kbnMuY29tCi9kbnMtcXVlcnk";

        private static async Task<int> InvokeCliAsync(params string[] args) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { args })!;
            return await task;
        }

        private static async Task RunStallingHttpServerAsync(int port, ManualResetEventSlim ready, CancellationToken token) {
#if NET8_0_OR_GREATER
            using var listener = new TcpListener(IPAddress.Loopback, port);
#else
            var listener = new TcpListener(IPAddress.Loopback, port);
#endif
            listener.Start();
            ready.Set();

            try {
                TcpClient client;
#if NET8_0_OR_GREATER
                client = await listener.AcceptTcpClientAsync(token);
#else
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
                Task completed = await Task.WhenAny(acceptTask, Task.Delay(Timeout.Infinite, token));
                if (completed != acceptTask) {
                    throw new OperationCanceledException(token);
                }

                client = acceptTask.Result;
#endif

                using (client)
                using (NetworkStream stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true)) {
                    while (!token.IsCancellationRequested) {
                        string? line = await reader.ReadLineAsync();
                        if (line is null || line.Length == 0) {
                            break;
                        }
                    }

                    await Task.Delay(Timeout.Infinite, token);
                }
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                // Expected during test cleanup after the client-side cancellation path is verified.
            } finally {
                listener.Stop();
            }
        }

        /// <summary>
        /// Ensures validation preserves file line context for valid and invalid entries.
        /// </summary>
        [Fact]
        public async Task ValidateManyAsync_FileEntries_ReturnsLineContext() {
            string resolverFile = Path.GetTempFileName();
            File.WriteAllText(
                resolverFile,
                "# comment\r\nudp@1.1.1.1:53,broken endpoint\r\n" + CloudflareStamp + "\r\n");

            try {
                ResolverEndpointValidationResult[] results = await EndpointParser.ValidateManyAsync(files: new[] { resolverFile });

                Assert.Equal(3, results.Length);
                Assert.Equal(2, results.Count(result => result.IsValid));
                Assert.Single(results, result => !result.IsValid);
                Assert.All(results, result => Assert.Equal(resolverFile, result.Source));
                Assert.Equal(2, results[0].LineNumber);
                Assert.Equal(2, results[1].LineNumber);
                Assert.Equal(3, results[2].LineNumber);
                Assert.Equal(Transport.Doh, results[2].Endpoint!.Transport);
            } finally {
                File.Delete(resolverFile);
            }
        }

        /// <summary>
        /// Ensures unreadable resolver files are reported as invalid sources.
        /// </summary>
        [Fact]
        public async Task ValidateManyAsync_UnreadableFile_ReturnsInvalidResult() {
            string resolverFile = Path.GetTempFileName();
            File.WriteAllText(resolverFile, "udp@1.1.1.1:53\r\n");

            try {
                using var locked = new FileStream(resolverFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

                ResolverEndpointValidationResult[] results = await EndpointParser.ValidateManyAsync(files: new[] { resolverFile });

                ResolverEndpointValidationResult result = Assert.Single(results);
                Assert.False(result.IsValid);
                Assert.Equal(resolverFile, result.Source);
                Assert.Equal(resolverFile, result.Entry);
                Assert.False(string.IsNullOrWhiteSpace(result.Error));
            } finally {
                File.Delete(resolverFile);
            }
        }

        /// <summary>
        /// Ensures malformed resolver file paths are reported per source and do not abort validation.
        /// </summary>
        [Fact]
        public async Task ValidateManyAsync_InvalidFilePath_ReturnsInvalidResultAndContinues() {
            const string invalidPath = "bad\0path";
            string resolverFile = Path.GetTempFileName();
            File.WriteAllText(resolverFile, "udp@1.1.1.1:53\r\n");

            try {
                ResolverEndpointValidationResult[] results = await EndpointParser.ValidateManyAsync(files: new[] { invalidPath, resolverFile });

                Assert.Equal(2, results.Length);
                Assert.False(results[0].IsValid);
                Assert.Equal(invalidPath, results[0].Source);
                Assert.Equal(invalidPath, results[0].Entry);
                Assert.False(string.IsNullOrWhiteSpace(results[0].Error));
                Assert.True(results[1].IsValid);
                Assert.Equal(resolverFile, results[1].Source);
            } finally {
                File.Delete(resolverFile);
            }
        }

        /// <summary>
        /// Ensures caller cancellation is not reported as a URL validation error.
        /// </summary>
        [Fact]
        public async Task ValidateManyAsync_UrlImport_PropagatesCancellation() {
            int port = TestUtilities.GetFreeTcpPort();
            using var ready = new ManualResetEventSlim(false);
            using var serverCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunStallingHttpServerAsync(port, ready, serverCts.Token);
            Assert.True(ready.Wait(TimeSpan.FromSeconds(2)));

            using var clientCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            try {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => EndpointParser.ValidateManyAsync(
                    urls: new[] { $"http://127.0.0.1:{port}/resolvers.txt" },
                    cancellationToken: clientCts.Token));
            } finally {
                serverCts.Cancel();
                await serverTask;
            }
        }

        /// <summary>
        /// Ensures CLI validation prints a readable report and exits non-zero when entries are invalid.
        /// </summary>
        [Fact]
        public async Task CliResolverValidate_PrintsTextReport() {
            string resolverFile = Path.GetTempFileName();
            File.WriteAllText(resolverFile, "udp@1.1.1.1:53\r\nbroken endpoint\r\n");

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--resolver-validate", "--resolver-file", resolverFile);

                Assert.Equal(1, exitCode);
                string text = output.ToString();
                Assert.Contains("Resolver Validation:", text, StringComparison.Ordinal);
                Assert.Contains("Valid: 1", text, StringComparison.Ordinal);
                Assert.Contains("Invalid: 1", text, StringComparison.Ordinal);
                Assert.Contains("valid", text, StringComparison.Ordinal);
                Assert.Contains("invalid", text, StringComparison.Ordinal);
                Assert.Contains(resolverFile + ":2", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
                File.Delete(resolverFile);
            }
        }

        /// <summary>
        /// Ensures CLI validation emits structured JSON for automation.
        /// </summary>
        [Fact]
        public async Task CliResolverValidate_Json_PrintsStructuredResults() {
            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--resolver-validate", "--probe-endpoint", CloudflareStamp, "--format", "json");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("\"Source\": \"inline\"", text, StringComparison.Ordinal);
                Assert.Contains("\"IsValid\": true", text, StringComparison.Ordinal);
                Assert.Contains("\"Transport\": \"Doh\"", text, StringComparison.Ordinal);
                Assert.Contains("\"Host\": \"mozilla.cloudflare-dns.com\"", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
            }
        }
    }
}
