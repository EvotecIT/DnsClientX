using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    public class DemoRecords {
        /// <summary>
        /// Demo for the specified domain name with the specified type and endpoint.
        /// </summary>
        /// <param name="domainName">Name of the domain.</param>
        /// <param name="type">The type.</param>
        /// <param name="endpoint">The endpoint.</param>
        public static async Task Demo(string domainName, DnsRecordType type, DnsEndpoint endpoint) {
            var Client = new ClientX(endpoint);
            Console.WriteLine($"> Resolving the {type} record on {domainName} using {endpoint}");
            var caaAnswer = await Client.ResolveAll(domainName, type);
            caaAnswer.DisplayToConsole();
        }
    }
}
