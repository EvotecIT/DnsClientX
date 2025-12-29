using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests alias handling for ResolveFilter APIs.
    /// </summary>
    public class ResolveFilterAliasTests {
        private static DnsResponse CreateResponse(string name) {
            return new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = name, Type = DnsRecordType.CNAME, TTL = 60, DataRaw = "alias.example.com" },
                    new DnsAnswer { Name = name, Type = DnsRecordType.TXT, TTL = 120, DataRaw = "v=spf1 include:example.com -all\nother=record" }
                }
            };
        }

        /// <summary>
        /// Ensures alias answers are kept alongside matching TXT lines when enabled.
        /// </summary>
        [Fact]
        public async Task ResolveFilter_IncludeAliases_KeepsCnameAndMatchingTxt() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (name, type, ct) => Task.FromResult(CreateResponse(name));

            var options = new ResolveFilterOptions(true);
            var response = await client.ResolveFilter("example.com", DnsRecordType.TXT, "v=spf1", options, retryOnTransient: false);

            Assert.NotNull(response.Answers);
            Assert.Equal(2, response.Answers.Length);
            Assert.Contains(response.Answers, answer => answer.Type == DnsRecordType.CNAME);
            Assert.Contains(response.Answers, answer => answer.Type == DnsRecordType.TXT && answer.Data == "v=spf1 include:example.com -all");
        }

        /// <summary>
        /// Ensures alias answers are not kept when alias inclusion is disabled.
        /// </summary>
        [Fact]
        public async Task ResolveFilter_ExcludeAliases_DropsCname() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (name, type, ct) => Task.FromResult(CreateResponse(name));

            var options = new ResolveFilterOptions(false);
            var response = await client.ResolveFilter("example.com", DnsRecordType.TXT, "v=spf1", options, retryOnTransient: false);

            Assert.Single(response.Answers);
            Assert.Equal(DnsRecordType.TXT, response.Answers[0].Type);
        }

        /// <summary>
        /// Ensures array-based ResolveFilter returns responses when only aliases match.
        /// </summary>
        [Fact]
        public async Task ResolveFilter_Array_IncludeAliases_ReturnsResponseWithOnlyCname() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (name, type, ct) => Task.FromResult(CreateResponse(name));

            var options = new ResolveFilterOptions(true);
            var responses = await client.ResolveFilter(new[] { "example.com" }, DnsRecordType.TXT, "nomatch", options, retryOnTransient: false);

            Assert.Single(responses);
            Assert.Single(responses[0].Answers);
            Assert.Equal(DnsRecordType.CNAME, responses[0].Answers[0].Type);    
        }

        /// <summary>
        /// Ensures empty filters still keep alias and requested type answers when enabled.
        /// </summary>
        [Fact]
        public async Task ResolveFilter_IncludeAliases_EmptyFilter_ReturnsAliasAndType() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (name, type, ct) => Task.FromResult(CreateResponse(name));

            var options = new ResolveFilterOptions(true);
            var response = await client.ResolveFilter("example.com", DnsRecordType.TXT, string.Empty, options, retryOnTransient: false);

            Assert.NotNull(response.Answers);
            Assert.Equal(2, response.Answers.Length);
            Assert.Contains(response.Answers, answer => answer.Type == DnsRecordType.CNAME);
            Assert.Contains(response.Answers, answer => answer.Type == DnsRecordType.TXT);
        }

        /// <summary>
        /// Ensures null filters are treated as empty when alias inclusion is enabled.
        /// </summary>
        [Fact]
        public async Task ResolveFilter_IncludeAliases_NullFilter_TreatedAsEmpty() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (name, type, ct) => Task.FromResult(CreateResponse(name));

            var options = new ResolveFilterOptions(true);
            string? filter = null;
#pragma warning disable CS8604 // Null is allowed for robustness testing.
            var response = await client.ResolveFilter("example.com", DnsRecordType.TXT, filter, options, retryOnTransient: false);
#pragma warning restore CS8604

            Assert.NotNull(response.Answers);
            Assert.Equal(2, response.Answers.Length);
            Assert.Contains(response.Answers, answer => answer.Type == DnsRecordType.CNAME);
            Assert.Contains(response.Answers, answer => answer.Type == DnsRecordType.TXT);
        }

        /// <summary>
        /// Ensures alias filtering still respects the filter when querying alias types.
        /// </summary>
        [Fact]
        public async Task ResolveFilter_IncludeAliases_AliasType_RespectsFilter() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            client.ResolverOverride = (name, type, ct) => Task.FromResult(CreateResponse(name));

            var options = new ResolveFilterOptions(true);
            var response = await client.ResolveFilter("example.com", DnsRecordType.CNAME, "nomatch", options, retryOnTransient: false);

            Assert.NotNull(response.Answers);
            Assert.Empty(response.Answers);
        }
    }
}
