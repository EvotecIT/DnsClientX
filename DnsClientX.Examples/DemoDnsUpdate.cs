using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Example usage of <see cref="ClientX"/> DNS UPDATE methods.
    /// </summary>
    internal class DemoDnsUpdate {
        /// <summary>
        /// Demonstrates adding a record to a zone.
        /// </summary>
        public static async Task ExampleAdd() {
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP);
            await client.UpdateRecordAsync("example.com", "www.example.com", DnsRecordType.A, "1.2.3.4");
        }

        /// <summary>
        /// Demonstrates adding a record to a zone with a custom TTL.
        /// </summary>
        public static async Task ExampleAddWithTtl() {
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP);
            await client.UpdateRecordAsync("example.com", "www.example.com", DnsRecordType.A, "1.2.3.4", 600);
        }

        /// <summary>
        /// Demonstrates deleting a record from a zone.
        /// </summary>
        public static async Task ExampleDelete() {
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP);
            await client.DeleteRecordAsync("example.com", "www.example.com", DnsRecordType.A);
        }
    }
}
