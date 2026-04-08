using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests CLI capability-report output.
    /// </summary>
    [Collection("NoParallel")]
    public class CliCapabilityTests {
        private static async Task<int> InvokeCliAsync(params string[] args) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { args })!;
            return await task;
        }

        /// <summary>
        /// Ensures capability mode emits the shared text report.
        /// </summary>
        [Fact]
        public async Task CapabilitiesMode_PrintsTextReport() {
            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--capabilities");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Transport Capabilities:", text, StringComparison.Ordinal);
                Assert.Contains("DNS over HTTP/3", text, StringComparison.Ordinal);
                Assert.Contains("DNS over QUIC", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
            }
        }

        /// <summary>
        /// Ensures capability mode emits JSON when requested.
        /// </summary>
        [Fact]
        public async Task CapabilitiesMode_Json_PrintsStructuredReport() {
            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--capabilities", "--format", "json");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("\"RequestFormat\": \"DnsOverHttp3\"", text, StringComparison.Ordinal);
                Assert.Contains("\"RequestFormat\": \"DnsOverQuic\"", text, StringComparison.Ordinal);
                Assert.Contains("\"Supported\":", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
            }
        }
    }
}
