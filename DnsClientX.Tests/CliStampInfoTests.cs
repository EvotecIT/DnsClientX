using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests CLI DNS stamp inspection mode.
    /// </summary>
    [Collection("NoParallel")]
    public class CliStampInfoTests {
        private const string CloudflareStamp = "sdns://AgUAAAAAAAAABzEuMS4xLjEAGm1vemlsbGEuY2xvdWRmbGFyZS1kbnMuY29tCi9kbnMtcXVlcnk";

        private static async Task<int> InvokeCliAsync(params string[] args) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { args })!;
            return await task;
        }

        /// <summary>
        /// Ensures stamp mode prints a readable no-network description.
        /// </summary>
        [Fact]
        public async Task StampInfo_PrintsTextDescription() {
            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--stamp-info", CloudflareStamp);

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("DNS Stamp:", text, StringComparison.Ordinal);
                Assert.Contains("Transport: Doh", text, StringComparison.Ordinal);
                Assert.Contains("Host: mozilla.cloudflare-dns.com", text, StringComparison.Ordinal);
                Assert.Contains("DoH URL: https://mozilla.cloudflare-dns.com/dns-query", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
            }
        }

        /// <summary>
        /// Ensures stamp mode can emit JSON for automation.
        /// </summary>
        [Fact]
        public async Task StampInfo_Json_PrintsStructuredDescription() {
            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--stamp-info", CloudflareStamp, "--format", "json");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("\"Transport\": \"Doh\"", text, StringComparison.Ordinal);
                Assert.Contains("\"RequestFormat\": \"DnsOverHttps\"", text, StringComparison.Ordinal);
                Assert.Contains("\"Host\": \"mozilla.cloudflare-dns.com\"", text, StringComparison.Ordinal);
                Assert.Contains("\"DnsSecOk\": true", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
            }
        }

        /// <summary>
        /// Ensures stamp mode rejects query-only switches.
        /// </summary>
        [Fact]
        public async Task StampInfo_RejectsQuerySwitches() {
            using var error = new StringWriter();
            TextWriter originalError = Console.Error;
            try {
                Console.SetError(error);

                int exitCode = await InvokeCliAsync("--stamp-info", CloudflareStamp, "--short");

                Assert.Equal(1, exitCode);
                Assert.Contains("Stamp mode supports only --stamp-info", error.ToString(), StringComparison.Ordinal);
            } finally {
                Console.SetError(originalError);
            }
        }
    }
}
