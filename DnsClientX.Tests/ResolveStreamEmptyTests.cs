using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests <see cref="ClientX.ResolveStream"/> when provided with no domain names.
    /// </summary>
    public class ResolveStreamEmptyTests {
        /// <summary>
        /// Ensures an empty result is returned when no names are supplied.
        /// </summary>
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
