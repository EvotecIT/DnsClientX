using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests verifying resolution starting at DNS root servers.
    /// </summary>
    public class ResolveFromRootTests {
        [Fact(Skip = "External dependency - requires root servers")] // network unreachable in CI
        public async Task ShouldResolveARecordFromRoot() {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.A, DnsEndpoint.RootServer);
            Assert.NotEmpty(response.Answers);
            foreach (var ans in response.Answers) {
                Assert.Equal(DnsRecordType.A, ans.Type);
            }
        }
    }
}
