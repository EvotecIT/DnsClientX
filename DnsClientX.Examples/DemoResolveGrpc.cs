using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Example illustrating DNS resolution over gRPC transport.
    /// </summary>
    internal static class DemoResolveGrpc {
        public static async Task Example() {
            using var client = new ClientX("localhost", DnsRequestFormat.DnsOverGrpc) {
                EndpointConfiguration = { Port = 50051 }
            };

            HelpersSpectre.AddLine("Resolve", "example.com", DnsRecordType.A, "localhost", DnsRequestFormat.DnsOverGrpc);
            var response = await client.Resolve("example.com", DnsRecordType.A);
            response.DisplayTable();
        }
    }
}
