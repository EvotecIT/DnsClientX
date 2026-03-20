using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests verifying resolution starting at DNS root servers.
    /// </summary>
    public class ResolveFromRootTests {
        /// <summary>
        /// Queries the root servers directly to resolve an A record.
        /// </summary>
        [RealDnsFact]
        public async Task ShouldResolveARecordFromRoot() {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.A, DnsEndpoint.RootServer);
            Assert.NotEmpty(response.Answers);
            foreach (var ans in response.Answers) {
                Assert.Equal(DnsRecordType.A, ans.Type);
            }
        }
    }
}
