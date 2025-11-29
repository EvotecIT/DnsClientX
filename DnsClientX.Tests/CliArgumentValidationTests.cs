using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests validation of command line arguments for the CLI application.
    /// </summary>
    [Collection("NoParallel")]
    public class CliArgumentValidationTests {
        /// <summary>
        /// Running the CLI with an unknown option should display help and return an error code.
        /// </summary>
        [Fact]
        public async Task UnknownOption_ShowsHelpAndReturnsError() {
            var result = await RunCli("--unknown");
            Assert.Equal(1, result.exitCode);
            string combined = result.output + result.error;
            Assert.Contains("Unknown argument: --unknown", combined);
            Assert.Contains("Usage: DnsClientX.Cli", combined);
        }

        [Fact]
        public async Task InvalidRecordType_ShowsMessageAndReturnsError() {
            var result = await RunCli("-t", "InvalidType", "example.com");
            Assert.Equal(1, result.exitCode);
            Assert.Contains("Invalid record type: InvalidType", result.error);
        }

        [Fact]
        public async Task InvalidEndpoint_ShowsMessageAndReturnsError() {
            var result = await RunCli("--endpoint", "InvalidEndpoint", "example.com");
            Assert.Equal(1, result.exitCode);
            Assert.Contains("Invalid endpoint: InvalidEndpoint", result.error);
        }

        [Fact]
        public async Task InvalidUpdateRecordType_ShowsMessageAndReturnsError() {
            var result = await RunCli("--update", "example.com", "record", "BadType", "data");
            Assert.Equal(1, result.exitCode);
            Assert.Contains("Invalid record type for update: BadType", result.error);
        }

        private static async Task<(int exitCode, string output, string error)> RunCli(params string[] args) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;

            using var output = new StringWriter();
            using var error = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            try {
                Console.SetOut(output);
                Console.SetError(error);
                Task<int> task = (Task<int>)main.Invoke(null, new object[] { args })!;
                int exitCode = await task;
                return (exitCode, output.ToString(), error.ToString());
            } finally {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
    }
}
