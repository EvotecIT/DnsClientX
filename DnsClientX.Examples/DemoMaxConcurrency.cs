using System;
using System.Linq;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates how to cap parallelism when resolving multiple names.
    /// </summary>
    public static class DemoMaxConcurrency {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);

            // By default, no cap is applied (null). Set to limit in-flight queries.
            client.EndpointConfiguration.MaxConcurrency = 4;

            var names = new[] {
                "one.example", "two.example", "three.example",
                "four.example", "five.example", "six.example"
            };

            var responses = await client.Resolve(names, DnsRecordType.A);

            Console.WriteLine($"Resolved {responses.Length} names with max concurrency {client.EndpointConfiguration.MaxConcurrency}.");
            foreach (var (name, i) in names.Select((n, i) => (n, i))) {
                var answers = string.Join(", ", responses[i].Answers.Select(a => a.Data));
                Console.WriteLine($"{name} -> {answers}");
            }
        }
    }
}

