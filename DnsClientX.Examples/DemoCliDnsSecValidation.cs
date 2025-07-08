using System.Reflection;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates invoking the command line interface with DNSSEC validation enabled.
    /// </summary>
    internal static class DemoCliDnsSecValidation {
        public static async Task Example() {
            var assembly = Assembly.Load("DnsClientX.Cli");
            var programType = assembly.GetType("DnsClientX.Cli.Program")!;
            var main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            await (Task<int>)main.Invoke(null, new object[] { new[] { "--dnssec", "--validate-dnssec", "--wire-post", "evotec.pl" } })!;
        }
    }
}
