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

        /// <summary>
        /// Ensures benchmark mode infers PTR when a positional domain is an IP literal.
        /// </summary>
        [Fact]
        public void Benchmark_PositionalIp_DefaultsToPtr() {
            object cliOptions = ParseCliOptions("--benchmark", "1.1.1.1");
            Type optionsType = cliOptions.GetType();

            DnsRecordType recordType = (DnsRecordType)optionsType.GetProperty("RecordType")!.GetValue(cliOptions)!;
            Assert.Equal(DnsRecordType.PTR, recordType);
        }

        /// <summary>
        /// Ensures shared query-run options preserve CLI wire-post and port override settings.
        /// </summary>
        [Fact]
        public void CreateQueryRunOptions_PreservesWirePostAndPortOverride() {
            string? originalPort = Environment.GetEnvironmentVariable("DNSCLIENTX_CLI_PORT");
            Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", "8053");

            try {
                object cliOptions = ParseCliOptions("--benchmark", "--wire-post", "example.com");
                var assembly = Assembly.Load("DnsClientX.Cli");
                Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
                MethodInfo createQueryRunOptions = programType.GetMethod("CreateQueryRunOptions", BindingFlags.NonPublic | BindingFlags.Static)!;

                ResolverQueryRunOptions runOptions = (ResolverQueryRunOptions)createQueryRunOptions.Invoke(null, new[] { cliOptions })!;

                Assert.True(runOptions.ForceDohWirePost);
                Assert.Equal(8053, runOptions.PortOverride);
            } finally {
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", originalPort);
            }
        }

        /// <summary>
        /// Ensures the CLI rejects the built-in Custom endpoint and directs callers to explicit endpoint syntax.
        /// </summary>
        [Fact]
        public void ParseCliOptions_RejectsCustomBuiltInEndpoint() {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo tryParseArgs = programType.GetMethod("TryParseArgs", BindingFlags.NonPublic | BindingFlags.Static)!;

            object?[] parameters = { new[] { "--endpoint", "Custom", "example.com" }, null, null, null };
            bool success = (bool)tryParseArgs.Invoke(null, parameters)!;

            Assert.False(success);
            Assert.Contains("not supported in the CLI", parameters[2]?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static object ParseCliOptions(params string[] args) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo tryParseArgs = programType.GetMethod("TryParseArgs", BindingFlags.NonPublic | BindingFlags.Static)!;

            object?[] parameters = { args, null, null, null };
            bool success = (bool)tryParseArgs.Invoke(null, parameters)!;

            Assert.True(success, parameters[2]?.ToString());
            Assert.NotNull(parameters[1]);
            return parameters[1]!;
        }
    }
}
