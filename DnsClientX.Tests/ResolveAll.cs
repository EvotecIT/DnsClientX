namespace DnsClientX.Tests {
    public class ResolveAll {
        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]



        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        public async Task ShouldWorkForTXT(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            DnsAnswer[] aAnswers = await Client.ResolveAll("github.com", DnsRecordType.TXT).ConfigureAwait(false);
            foreach (DnsAnswer answer in aAnswers) {
                Assert.True(answer.Name == "github.com");
                Assert.True((bool)(answer.Type == DnsRecordType.TXT));
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]



        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        public async Task ShouldWorkForA(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            DnsAnswer[] aAnswers = await Client.ResolveAll("evotec.pl", DnsRecordType.A).ConfigureAwait(false);
            foreach (DnsAnswer answer in aAnswers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True((bool)(answer.Type == DnsRecordType.A));
            }
        }

        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]



        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        public void ShouldWorkForTXT_Sync(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            DnsAnswer[] aAnswers = Client.ResolveAllSync("github.com", DnsRecordType.TXT);
            foreach (DnsAnswer answer in aAnswers) {
                Assert.True(answer.Name == "github.com");
                Assert.True((bool)(answer.Type == DnsRecordType.TXT));
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]



        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        public void ShouldWorkForA_Sync(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            DnsAnswer[] aAnswers = Client.ResolveAllSync("evotec.pl", DnsRecordType.A);
            foreach (DnsAnswer answer in aAnswers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True((bool)(answer.Type == DnsRecordType.A));
            }
        }
    }
}
