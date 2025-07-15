using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Integration tests covering the command line application.
    /// </summary>
    [Collection("NoParallel")]
    public class CliIntegrationTests {
        /// <summary>
        /// Ensures the CLI can execute without leaving open sockets.
        /// </summary>
        [Fact]
        public async Task CliRunsWithoutLeavingSockets() {
            ClientX.DisposalCount = 0;
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { "localhost" } })!;
            int exitCode = await task;
            Assert.Equal(0, exitCode);
            Assert.True(ClientX.DisposalCount >= 1, $"Expected at least one disposal but was {ClientX.DisposalCount}");
        }

        /// <summary>
        /// The --type option should be case-insensitive.
        /// </summary>
        [Theory]
        [InlineData("--type")]
        [InlineData("--TYPE")]
        public async Task TypeOption_IsCaseInsensitive(string option) {
            ClientX.DisposalCount = 0;
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { option, "A", "localhost" } })!;
            int exitCode = await task;
            Assert.Equal(0, exitCode);
            Assert.True(ClientX.DisposalCount >= 1,
                $"Expected at least one disposal but was {ClientX.DisposalCount}");
            ClientX.DisposalCount = 0;
        }

        /// <summary>
        /// Validates that the --wire-post switch executes successfully.
        /// </summary>
        [Fact]
        public async Task WirePostOption_Executes() {
            ClientX.DisposalCount = 0;
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { "--wire-post", "localhost" } })!;
            int exitCode = await task;
            Assert.Equal(0, exitCode);
            Assert.True(ClientX.DisposalCount >= 1);
        }

        /// <summary>
        /// Ensures that retry statistics are printed to stdout.
        /// </summary>
        [Fact]
        public async Task Cli_DisplaysRetryCount() {
            ClientX.DisposalCount = 0;

            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;

            using var sw = new StringWriter();
            TextWriter original = Console.Out;
            Console.SetOut(sw);
            try {
                Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { "localhost" } })!;
                int exitCode = await task;
                Assert.Equal(0, exitCode);
                string output = sw.ToString();
                Assert.Contains("retries", output, StringComparison.OrdinalIgnoreCase);
            } finally {
                Console.SetOut(original);
            }

            Assert.True(ClientX.DisposalCount >= 1);
        }
    }
}
