using System.Diagnostics;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests covering the <see cref="ClientX.ResolveAll(string,DnsRecordType,bool,bool,bool,int,int,System.Threading.CancellationToken)"/>
    /// API using different endpoints.
    /// </summary>
    public class ResolveAll {
        /// <summary>
        /// Resolves TXT records from the specified endpoint using asynchronous API.
        /// </summary>
        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
#if DNS_OVER_QUIC
        [InlineData(DnsEndpoint.CloudflareQuic)]
        [InlineData(DnsEndpoint.GoogleQuic)]
#endif
        public async Task ShouldWorkForTXT(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            DnsAnswer[] aAnswers = await Client.ResolveAll("github.com", DnsRecordType.TXT);
            foreach (DnsAnswer answer in aAnswers) {
                Assert.True(answer.Name == "github.com");
                Assert.True((bool)(answer.Type == DnsRecordType.TXT));
                Assert.True(answer.Data.Length > 0);
            }
        }

        /// <summary>
        /// Resolves A records from the specified endpoint using asynchronous API.
        /// </summary>
        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]



        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
#if DNS_OVER_QUIC
        [InlineData(DnsEndpoint.CloudflareQuic)]
        [InlineData(DnsEndpoint.GoogleQuic)]
#endif
        public async Task ShouldWorkForA(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            DnsAnswer[] aAnswers = await Client.ResolveAll("evotec.pl", DnsRecordType.A);
            foreach (DnsAnswer answer in aAnswers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True((bool)(answer.Type == DnsRecordType.A));
            }
        }

        /// <summary>
        /// Resolves TXT records synchronously using the specified endpoint.
        /// </summary>
        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
#if DNS_OVER_QUIC
        [InlineData(DnsEndpoint.CloudflareQuic)]
        [InlineData(DnsEndpoint.GoogleQuic)]
#endif
        public void ShouldWorkForTXT_Sync(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            DnsAnswer[] aAnswers = Client.ResolveAllSync("github.com", DnsRecordType.TXT);
            foreach (DnsAnswer answer in aAnswers) {
                Assert.True(answer.Name == "github.com");
                Assert.True((bool)(answer.Type == DnsRecordType.TXT));
                Assert.True(answer.Data.Length > 0);
            }
        }

        /// <summary>
        /// Resolves A records synchronously using the specified endpoint.
        /// </summary>
        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
#if DNS_OVER_QUIC
        [InlineData(DnsEndpoint.CloudflareQuic)]
        [InlineData(DnsEndpoint.GoogleQuic)]
#endif
        public void ShouldWorkForA_Sync(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            DnsAnswer[] aAnswers = Client.ResolveAllSync("evotec.pl", DnsRecordType.A);
            foreach (DnsAnswer answer in aAnswers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True((bool)(answer.Type == DnsRecordType.A));
            }
        }

        /// <summary>
        /// Ensures no delay occurs when <c>maxRetries</c> is set to one.
        /// </summary>
        [Fact]
        public async Task ShouldNotDelayWhenMaxRetriesIsOne() {
            using var Client = new ClientX(DnsEndpoint.Cloudflare);
            var sw = Stopwatch.StartNew();
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => Client.ResolveAll(string.Empty, DnsRecordType.A, retryOnTransient: true, maxRetries: 1, retryDelayMs: 200));
            sw.Stop();

            Assert.InRange(sw.ElapsedMilliseconds, 0, 100);
        }
    }
}
