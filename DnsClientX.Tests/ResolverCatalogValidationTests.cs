using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
