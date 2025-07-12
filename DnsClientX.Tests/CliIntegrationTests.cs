using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    [Collection("NoParallel")]
    public class CliIntegrationTests {
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
            Assert.Equal(1, ClientX.DisposalCount);
            ClientX.DisposalCount = 0;
        }

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
    }
}
