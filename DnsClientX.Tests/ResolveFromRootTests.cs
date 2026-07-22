using System;
using System.IO;
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
            Assert.True(response.QNameMinimizedQueryCount > 0,
                "Iterative resolution did not report any minimized delegation queries.");
        }

        /// <summary>Authenticates the live root DNSKEY set and persists managed RFC 5011 state.</summary>
        [RealDnsFact]
        public async Task ShouldRefreshManagedRootTrustAnchors() {
            string directory = Path.Combine(Path.GetTempPath(),
                "DnsClientX-Rfc5011-Live-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "root-anchors.json");
            try {
                using var client = new ClientX(DnsEndpoint.RootServer);
                client.EndpointConfiguration.Rfc5011TrustAnchorStorePath = path;

                DnsSecTrustAnchorRefreshResult result = await client.RefreshRootTrustAnchorsAsync();

                Assert.True(result.Succeeded, result.Response.DnsSecValidationMessage);
                Assert.NotNull(result.Snapshot);
                Assert.Contains(result.Snapshot!.Anchors,
                    anchor => anchor.State == DnsSecTrustAnchorState.Valid);
                Assert.True(result.Snapshot.NextRefreshUtc > DateTimeOffset.UtcNow);
                Assert.True(File.Exists(path));
            } finally {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }
    }
}
