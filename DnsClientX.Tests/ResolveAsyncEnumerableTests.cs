using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class ResolveAsyncEnumerableTests {
        [Fact]
        public async Task ResolveAsyncEnumerable_MultipleResponses() {
            using var client = new ClientX(DnsEndpoint.System);
            var names = new[] { "github.com", "microsoft.com" };
            var types = new[] { DnsRecordType.A, DnsRecordType.MX };
            int count = 0;

            await foreach (var response in client.ResolveAsyncEnumerable(names, types, retryOnTransient: false)) {
                Assert.NotNull(response);
                Assert.NotNull(response.Answers);
                count++;
            }

            Assert.Equal(names.Length * types.Length, count);
        }

        [Fact]
        public async Task ResolveAsyncEnumerable_EmptyNames_YieldsNothing() {
            using var client = new ClientX(DnsEndpoint.System);
            var results = new List<DnsResponse>();

            await foreach (var response in client.ResolveAsyncEnumerable(System.Array.Empty<string>(), new[] { DnsRecordType.A }, retryOnTransient: false)) {
                results.Add(response);
            }

            Assert.Empty(results);
        }
    }
}
