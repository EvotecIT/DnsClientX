using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class ResolveRootCancellationTests {
        [Fact]
        public async Task ResolveFromRoot_CancelsEarly() {
            using var client = new ClientX();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var response = await client.ResolveFromRoot("example.com", cancellationToken: cts.Token);
            Assert.NotEqual(DnsResponseCode.NoError, response.Status);
            Assert.NotNull(response.Error);
        }
    }
}
