using System;

namespace DnsClientX.Examples {
    /// <summary>
    /// Example that prints all available <see cref="DnsEndpoint"/> values
    /// with their descriptions.
    /// </summary>
    internal static class DemoListDnsEndpoints {
        public static void Example() {
            foreach (var (endpoint, description) in DnsEndpointExtensions.GetAllWithDescriptions()) {
                Console.WriteLine($"{endpoint,-20} {description}");
            }
        }
    }
}
