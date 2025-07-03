using System;
using System.Threading.Tasks;
using Spectre.Console;

namespace DnsClientX.Examples {
    internal class DemoServiceDiscovery {
        public static async Task Example() {
            var client = new ClientX();
            var services = await client.DiscoverServices("test");
            foreach (var sd in services) {
                AnsiConsole.MarkupLine($"[green]{sd.ServiceName}[/] -> {sd.Host}:{sd.Port}");
                foreach (var kv in sd.Txt) {
                    AnsiConsole.MarkupLine($"  [blue]{kv.Key}[/]: {kv.Value}");
                }
            }
        }
    }
}
