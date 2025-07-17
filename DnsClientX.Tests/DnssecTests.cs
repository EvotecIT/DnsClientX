using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests querying DNSSEC-enabled records.
    /// </summary>
    public class DnssecTests {
        /// <summary>
        /// Resolves DNSKEY records with DNSSEC enabled.
        /// </summary>
        [Theory]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.Google)]
        public async Task QueryDnskey_WithDnssec(DnsEndpoint endpoint) {
            using var client = new ClientX(endpoint);
            DnsResponse response = await client.Resolve("evotec.pl", DnsRecordType.DNSKEY, requestDnsSec: true);
            Assert.NotEmpty(response.Answers);
            Assert.Contains(response.Answers, a => a.Type == DnsRecordType.DNSKEY);
            Assert.True(response.AuthenticData || response.Answers.Any(a => a.Type == DnsRecordType.RRSIG));
        }

        /// <summary>
        /// Resolves DS records with DNSSEC enabled.
        /// </summary>
        [Theory]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.Google)]
        public async Task QueryDs_WithDnssec(DnsEndpoint endpoint) {
            using var client = new ClientX(endpoint);
            DnsResponse response = await client.Resolve("evotec.pl", DnsRecordType.DS, requestDnsSec: true);
            Assert.NotEmpty(response.Answers);
            Assert.Contains(response.Answers, a => a.Type == DnsRecordType.DS);
            Assert.True(response.AuthenticData || response.Answers.Any(a => a.Type == DnsRecordType.RRSIG));
        }
    }
}
