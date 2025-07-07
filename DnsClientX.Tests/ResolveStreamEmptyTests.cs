using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class ResolveStreamEmptyTests {
        [Fact]
        public async Task ResolveStream_NoNames_YieldsNoResults() {
            using var client = new ClientX(DnsEndpoint.System);
            var results = new List<DnsResponse>();
            await foreach (var response in client.ResolveStream(System.Array.Empty<string>(), new[] { DnsRecordType.A }, retryOnTransient: false)) {
                results.Add(response);
            }
            Assert.Empty(results);
        }
    }
}
