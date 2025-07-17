using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests that concurrent resolve operations do not throw errors.
    /// </summary>
    public class ResolveConcurrencyTests {
        /// <summary>
        /// Resolves in parallel and ensures each result contains answers.
        /// </summary>
        [Fact]
        public async Task ShouldResolveConcurrentlyWithoutErrors() {
            using var client = new ClientX(DnsEndpoint.System);
            client.ResolverOverride = (name, type, ct) =>
                Task.FromResult(new DnsResponse {
                    Answers = new[] {
                        new DnsAnswer {
                            Name = "example.com",
                            Type = DnsRecordType.A,
                            DataRaw = "127.0.0.1"
                        }
                    }
                });

            var tasks = Enumerable.Range(0, 10)
                .Select(_ => client.Resolve("example.com", DnsRecordType.A));

            var results = await Task.WhenAll(tasks);

            Assert.Equal(10, results.Length);
            Assert.All(results, r => Assert.NotNull(r.Answers));
        }
    }
}
