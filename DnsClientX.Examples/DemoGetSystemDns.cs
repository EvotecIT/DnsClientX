using System;

namespace DnsClientX.Examples {
    /// <summary>
    /// Example that prints DNS servers discovered on the system.
    /// </summary>
    internal static class DemoGetSystemDns {
        public static void Example() {
            var servers = SystemInformation.GetDnsFromActiveNetworkCard(refresh: true);
            Console.WriteLine("System DNS servers:");
            foreach (var server in servers) {
                Console.WriteLine($" - {server}");
            }
        }
    }
}
