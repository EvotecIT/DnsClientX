using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Integration tests covering the command line application.
    /// </summary>
    [Collection("DisposalTests")]
    public class CliIntegrationTests {
        /// <summary>
        /// Ensures the CLI can execute without leaving open sockets.
        /// </summary>
        [Fact]
        public async Task CliRunsWithoutLeavingSockets() {
            var initialCount = ClientX.DisposalCount;
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { "localhost" } })!;
            int exitCode = await task;
            Assert.Equal(0, exitCode);

            await Task.Delay(50); // Wait for async disposal
            var finalCount = ClientX.DisposalCount;
            Assert.True(finalCount - initialCount >= 1, $"Expected at least one disposal but was {finalCount - initialCount}");
        }

        /// <summary>
        /// The --type option should be case-insensitive.
        /// </summary>
        [Theory]
        [InlineData("--type")]
        [InlineData("--TYPE")]
        public async Task TypeOption_IsCaseInsensitive(string option) {
            var initialCount = ClientX.DisposalCount;
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { option, "A", "localhost" } })!;
            int exitCode = await task;
            Assert.Equal(0, exitCode);

            await Task.Delay(50); // Wait for async disposal
            var finalCount = ClientX.DisposalCount;
            Assert.True(finalCount - initialCount >= 1,
                $"Expected at least one disposal but was {finalCount - initialCount}");
        }

        /// <summary>
        /// Validates that the --wire-post switch executes successfully.
        /// </summary>
        [Fact]
        public async Task WirePostOption_Executes() {
            var initialCount = ClientX.DisposalCount;
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { "--wire-post", "localhost" } })!;
            int exitCode = await task;
            Assert.Equal(0, exitCode);

            await Task.Delay(50); // Wait for async disposal
            var finalCount = ClientX.DisposalCount;
            Assert.True(finalCount - initialCount >= 1);
        }

        /// <summary>
        /// Ensures that retry statistics are printed to stdout.
        /// </summary>
        [Fact]
        public async Task Cli_DisplaysRetryCount() {
            var initialCount = ClientX.DisposalCount;

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

            await Task.Delay(50); // Wait for async disposal
            var finalCount = ClientX.DisposalCount;
            Assert.True(finalCount - initialCount >= 1);
        }
    }
}
