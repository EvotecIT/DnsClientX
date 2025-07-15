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
                Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { "--unknown" } })!;
                int exitCode = await task;
                Assert.Equal(1, exitCode);
                string combined = output.ToString() + error.ToString();
                Assert.Contains("Unknown argument: --unknown", combined);
                Assert.Contains("Usage: DnsClientX.Cli", combined);
            } finally {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
    }
}
