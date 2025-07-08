using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates resolving various DNS record types against a chosen
    /// <see cref="DnsEndpoint"/>.
    /// </summary>
    public class DemoRecords {
        /// <summary>
        /// Demo for the specified domain name with the specified type and endpoint.
        /// </summary>
        /// <param name="domain">Name of the domain.</param>
        /// <param name="recordType">The type.</param>
        /// <param name="endpoint">The endpoint.</param>
        public static async Task Demo(string domain, DnsRecordType recordType, DnsEndpoint endpoint) {
            using (var client = new ClientX(endpoint)) {
                HelpersSpectre.AddLine("Resolve", domain, recordType, endpoint);
                var caaAnswer = await client.ResolveAll(domain, recordType);
                caaAnswer.DisplayTable();
            }
        }
    }
}
