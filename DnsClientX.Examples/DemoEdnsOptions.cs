using System.Threading.Tasks;

namespace DnsClientX.Examples;

/// <summary>
/// Demonstrates composing EDNS options using helper classes.
/// </summary>
internal class DemoEdnsOptions {
    /// <summary>Runs the example.</summary>
    public static async Task Example() {
        using var client = new ClientX(DnsEndpoint.Quad9) {
            EndpointConfiguration = {
                EdnsOptions = new EdnsOptions { EnableEdns = true }
            }
        };
        client.EndpointConfiguration.EdnsOptions!.Options.Add(new NsidOption());
        client.EndpointConfiguration.EdnsOptions.Options.Add(new EcsOption("192.0.2.1/24"));
        var response = await client.Resolve("example.com", DnsRecordType.A);
        response?.DisplayTable();
    }
}
