using System;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests TXT record filtering behavior without inventing resource-record boundaries.
    /// </summary>
    public class TxtRecordFilteringTests {
        /// <summary>
        /// Ensures content that resembles several records remains the single record supplied by the resolver.
        /// </summary>
        [Fact]
        public async Task ResolveFilter_PreservesWholeTxtRecord() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                DataRaw = "google-site-verification=abcgoogle-site-verification=defv=spf1 include:example.com -allfacebook-domain-verification=xyz"
            };

            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                Answers = new[] { answer },
                Questions = Array.Empty<DnsQuestion>()
            };

            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (_, _, _) => Task.FromResult(response);

            var filtered = await client.ResolveFilter("example.com", DnsRecordType.TXT, "v=spf1");

            Assert.NotNull(filtered.Answers);
            Assert.Single(filtered.Answers!);
            Assert.Equal(answer.DataRaw, filtered.Answers![0].Data);
        }
    }
}
