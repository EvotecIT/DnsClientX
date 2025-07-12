using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates how to publish the CLI as a single-file executable.
    /// </summary>
    internal static class DemoPublishSingleFile {
        /// <summary>Shows the publish command.</summary>
        public static Task Example() {
            Console.WriteLine("dotnet publish ../DnsClientX.Cli/DnsClientX.Cli.csproj -c Release -p:PublishSingleFile=true -r win-x64");
            return Task.CompletedTask;
        }
    }
}
