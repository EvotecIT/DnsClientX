using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
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
            Assert.Equal(1, ClientX.DisposalCount);
        }
    }
}
